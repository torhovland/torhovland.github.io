---
layout: post
title: Simple and elegant microservices authentication using JWT
subtitle: Poor man's delegation in .NET Core
---

OpenID Connect and OAuth2 are great solutions for secure authentication in web apps and mobile apps, and for securely allowing an app to access a backend service on behalf of the user. But they aren't all that fun to work with when you need the user identity to flow from one service to other services. In a microservices architecture, that scenario quickly becomes relevant. Sure, you don't always _need_ the user identity to flow to all the services. If you depend on a service to retrieve stock market data, that service probably doesn't need to know who you are or even care that a human is asking. If, on the other hand, a backend service for a mobile exercise app needs to ask a workout service to log a workout, that service needs to know which user to log the workout for. How do you do that securely? Of course you can't just post the username along with the workout data. That might work, however, if you also made sure that only trusted services were allowed to call the workout service. But you can't do that if you want all your microservices to provide external APIs.

What can the industry guidance on microservices tell us about this problem? Well, Sam Newman's [Building Microservices](https://www.amazon.com/Building-Microservices-Designing-Fine-Grained-Systems/dp/1491950358) doesn't give us a lot of hope: "This problem, unfortunately, has no simple answer, because it isn’t a simple problem." I think he's exaggerating, though, and that is the point of this article.

First of all, it is possible to do this using token based authentication, where for each service invocation, the client is responsible for getting an appropriate access token from the authorization server. When there is a chain of requests, as in the workout example above, each service in the middle of the chain must validate the incoming access token and then request a new access token where the scope, audience, client, issuer and role claims are such that the next service in the chain will accept it. This is called delegation, and Vittorio B. has a good description of it [here](https://blogs.msdn.microsoft.com/vbertocci/2008/09/07/delegation-or-traversing-multilayer-architectures/). But doing this with OAuth2 is problematic. It is one thing for a web app or mobile app to redirect a user to the authorization server for logging in and for accepting a consent screen. The user agent will just get redirected to the authorization server's pages, and when it is done, it will get redirected back to the app. There are client libraries that make all of this painless. It makes much less sense for a backend service to do this. When it is invoked, it is because some client has initiated a HTTP request that probably expects some JSON data back. Returning the HTML of a login page or consent page will definitely complicate things, as you need to instruct any client to expect this and deal with it properly. Then there is the question what the redirect URL from the authorization server should be in this case. Even if you were able to get all this working, it would probably result in an odd user experience, where the user would have to click through a bunch of consent screens for a number of totally unfamiliar microservices.

So what I am proposing instead is to do exactly what Vittorio is advising against: reusing tokens. The exchange between the frontend app and the first service should do standard OpenID Connect, but that first service should simply pass on the identity token to any other service. Before you dismiss that idea as crazy, let me first tell you that Vittorio thinks this is [a valid approach under certain conditions](http://www.cloudidentity.com/blog/2013/01/09/using-the-jwt-handler-for-implementing-poor-man-s-delegation-actas/). In summary, as long as your set of microservices belong to the same application suite, they are implemented as REST services, and you use JWT tokens, your are fine.

JWT, by the way, stand for JSON Web Tokens. Although they look encrypted, that's just a Base64 encoding. In reality, all the attributes (claims) of the token are visible to anyone. This is why you should treat tokens as sensitive. If you are unsure about any of the strings I use in the code below, or you simply want to debug something, it's useful to copy the bearer token out of an HTTP request from a web app involved in an OpenID Connect flow. You can use your browser's dev tools (F12) for that. Then just paste it into [jwt.io](http://jwt.io), a great tool for inspecting and creating tokens, and see all the decoded claims. Another great debugging tool is [Postman](https://www.getpostman.com/). You can use it to call your services with the Authorization HTTP header set to "Bearer <token>", and it will show you the exact errors in the case of a token validation error.

![Postman](https://github.com/torhovland/torhovland.github.io/raw/master/img/postman.png)

Anyway, let's see how to implement the poor man's delegation in .NET Core. Say we have a client calling service A, which in turn calls service B. We want both services to know the identity of the user. Note that this particular example uses Azure AD as authorization server, but you could really be using any authorization server capable of handing out JWTs. 

We'll start with service B, as that is the simplest one. The starting point is simply an ASP.NET Core Web API with no authentication specified. Next, you will need the NuGet package called Microsoft.AspNetCore.Authentication.JwtBearer.

In `Startup.cs`, just before `app.UseMvc();`, you will tell ASP.NET how to use JWT for authentication:

```c#
app.UseJwtBearerAuthentication(new JwtBearerOptions
{
    Authority = "https://login.windows.net/common",
    TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false, // The issuer may vary in a multitenant scenario.
        ValidateAudience = false, // Allowing passing a token among multiple services (audiences).
    }
});
```

Authority needs to refer to the authorization server. The JwtBearer library will use that to look up the .well-known/openid-configuration endpoint, which in turn is used to locate the public keys needed for validating the signatures of the JWTs. If your Azure AD app registration is configured as multi-tenant and your users will come from many different Azure AD directories, the issuer claim can be anything and you need to disable validation of that. If, on the other hand, all your users will be from one particular directory, you should not disable this validation, but rather specify which issuer to validate against:

```c#
app.UseJwtBearerAuthentication(new JwtBearerOptions
{
    Authority = "https://login.windows.net/common",
    TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = "https://sts.windows.net/3028e7f8-b71e-4b96-8a62-999999999999/",
        ValidateAudience = false, // Allowing passing a token among multiple services (audiences).
    }
});
```

The ValidIssuer above is how Azure AD will present it to you, where the GUID corresponds to the Directory ID where all your users are.

The reason ValidateAudience is disabled is to support the scenario where the various apps in your software suite retrieve their tokens from different app registrations at the authorization server. If you only have one app registration that all your apps collectively identify with, you can keep audience validation enabled and set the ValidAudience property. Remember, you can use [jwt.io](http://jwt.io) to find the necessary string.

So now that authentication has been configured, how do you deal with it in your controllers?

```c#
[Authorize]
[Route("api/[controller]")]
public class ValuesController : Controller
{
    // GET api/values
    [HttpGet]
    public IEnumerable<string> Get()
    {
        var user = HttpContext.User;
        var userId = user.FindFirst(ClaimTypes.NameIdentifier).Value;
        var email = user.FindFirst(ClaimTypes.Name).Value;
        var displayName = user.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
        return new[] { $"Service B has recognized you as {displayName} with email {email} and identity {userId}." };
    }
}
```

Note the `[Authorize]` attribute to actually secure your controller. Then it's just a matter of getting the User from the HttpContext and looking up the claims you need. NameIdentifier is basically a unique string, and should be used as your actual user ID. How email address and full name is presented does unfortunately vary between identity providers, and you would need to do some hacking with conditionals if you need to support more than one. Azure AD puts the full name into a claim called "name", but ClaimTypes.Name is mapped to "unique_name" claim, which contains the email address. Confusing, I know.

So how can one service call another one? Very simple, just take whatever token received from the client, and reuse it:

```c#
public async Task<IEnumerable<string>> Get()
{
    // ... extracting claims from HttpContext, like above
    var serviceA = new[] { $"Service A has recognized you as {displayName} with email {email} and identity {userId}." };

    // Now extract the token
    var token = HttpContext.Request.Headers["Authorization"][0].Substring("Bearer ".Length);

    // Calling service B with the token
    var client = new HttpClient();
    var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost:44371/api/values");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var response = await client.SendAsync(request);
    var serviceB = JToken.Parse(await response.Content.ReadAsStringAsync()).ToObject<IEnumerable<string>>();

    return serviceA.Concat(serviceB);
}
```

OK, so a service can call another service. But what do we need to do to enable the services to be at the receiving end of an OpenID Connect flow? Nothing! That's the beauty of it. When your microservices are supplemented with Javascript frontends that use the [implicit flow](https://openid.net/specs/openid-connect-core-1_0.html#ImplicitFlowAuth), they will simply receive the necessary JWT from the authorization server and use that to call your microservices. If you have server apps using the [code flow](https://openid.net/specs/openid-connect-core-1_0.html#CodeFlowAuth), it's the same thing, even if the flow is slightly different. They will get the token needed to call your services.

I won't show the code for the Javascript client here, as there is nothing special about it. If you're interested, see the GitHub link at the bottom. I just copied an [ADAL.js sample](https://github.com/Azure-Samples/active-directory-angularjs-singlepageapp) from Microsoft and modified it to present the sample strings from my services. But any OpenID Connect client would work.

![Client](https://github.com/torhovland/torhovland.github.io/raw/master/img/jwt-client.png)

That's basically it. While the full delegation mechanism, where each service asks the authorization service to convert the incoming access token to a new token, is elegant in a _completeness_ sense, the passing of a single JWT token as I have demonstrated here is certainly elegant in a _simplicity_ sense. It gives you secure identification of users among a set of microservices, and it does so in a way that is very simple to implement and maintain.

In order to run the client app, you will also have to add CORS to the backend services. Take a look at the sample code to see how to do that. It's available here:
https://github.com/torhovland/microservices-jwt-delegation