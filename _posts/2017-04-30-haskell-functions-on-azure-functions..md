---
layout: post
title: Haskell Functions on Azure Functions
subtitle: Running serverless, asynchronous, and fully autonomous microservices (not just Haskell).
bigimg: /img/haskell-azure-functions.jpg
---

Isn't it ironic how, when we think of microservices, we also tend to think about REST services? A microservices architecture is about autonomous services, in other words, services that are able to do their job without relying on help from other services. Earlier this month I wrote about [service to service delegation using JWT tokens](http://blog.hovland.xyz/2017-04-09-simple-and-elegant-microservices-authentication-using-JWT/). That is a useful technique, but in some way, it also represents the opposite of autonomous services.

No service is an island, though. They will need to make use of data coming from other sources. The question is just how to get it. As Jimmy Bogard [talks about here](https://vimeo.com/211218483), if one external visit to a web page results in a hierarchy of synchronous calls indirectly invoking almost all of your services, you're in trouble. Interestingly, he talks about a rule they introduced, where a service can delegate to one other level of services, but no more. 

Better than calling other services just-in-time, is to outfit your services with background, asynchronous operations that make sure that all the data a service needs is available to it in its own data store. Yes, this means duplication of data, but not necessarily blind duplication. Think of your services as separate [DDD Bounded Contexts](https://martinfowler.com/bliki/BoundedContext.html). A billing, a shipping and an accounting service probably all have their own idea of what a customer order is. Rather than duplicating the exact same data structures, they will pull the data they need, possibly aided by an [Anti Corruption Layer](http://www.markhneedham.com/blog/2009/07/07/domain-driven-design-anti-corruption-layer/).

So how do you run those background operations on Azure? Well, you can always spin up virtual machines or Docker containers, but there are other options. One of them is [WebJobs](https://docs.microsoft.com/en-us/azure/app-service-web/web-sites-create-web-jobs). Another is [Functions](https://azure.microsoft.com/en-us/services/functions/), which is actually built on WebJobs, but can run on a managed hosting environment where instantiation and scaling is taken care of, and you don't need to manage your own App Service (although you can if you wish). Further, the managed environment offers a number of [triggers](https://docs.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings), such as messages on a topic, a queue or a blob, and gives you easy access to inputs and outputs on those topics, queues and blobs, plus HTTP, databases and even email and SMS.

Azure Functions provide first-class support to languages such as C#, F#, Javascript, Python and PHP, but it also supports scripts, which in turn enables us to run any executable. We will take advantage of that and run a Haskell service. It will get triggered by incoming messages in an [Azure Storage Queue](https://azure.microsoft.com/en-us/services/storage/queues/), do some processing, and return an output message in another queue. The business example I've chosen is a real bread-and-butter operation from the recruitment domain, the much loved [Fizz Buzz Test](http://wiki.c2.com/?FizzBuzzTest). If you've been to _any_ programming job interview, you know what I'm talking about. You're supposed to replace numbers that are divisible by 3 with _Fizz_, numbers divisible by 5 with _Buzz_, and numbers divisible by 3*5 with _FizzBuzz_. Any other numbers you leave unchanged.

# The Haskell Function

Why Haskell? No particular reason, it just serves to demonstrate that you can run any executable compiled from any language as an Azure Function. I'm sure you could solve the Fizz Buzz Test using F#, maybe even C#. That said, there are certainly more complex domains where you would value Haskell's stronger type system and distinction between _pure_ and _impure_ functions. By the way, in the real world you would probably not want to enqueue stored messages, spin up a hosted environment, run a script, load an executable and enqueue another message just to calculate one Fizz Buzz number like I do here. A more realistic use case would be a service that pre-calculates some statistics that need to be immediately available on request, based on changes (events) from other services.

Don't freak out if you don't read Haskell, I will talk you through it, and it's really quite simple and readable. This is the function we would like to run:

```haskell
fizzBuzz :: Integer -> String
fizzBuzz n | fizzBuzzCombo n == "" = show n
           | otherwise             = show n ++ " becomes " ++ fizzBuzzCombo n
  where
    fizzBuzzCombo n = fizz n ++ buzz n
    fizz = anyzz "Fizz" 3
    buzz = anyzz "Buzz" 5
    anyzz word factor n | n `mod` factor == 0 = word
                        | otherwise           = ""
```

The first line is a function definition that tells us that `fizzBuzz` takes an `Integer` and returns a `String`. You don't really need to add definitions like this. The type inferencing in Haskell will mostly figure it out on its own, and your code will be just as strongly typed, but it is good practice, because it ensures that you and the compiler have the same idea about the involved types (a little bit like a unit test, or perhaps more like a code contract).

The next two lines are the function itself, on the highest abstraction level. Imagine there is a function `fizzBuzzCombo` that will return the appropriate _Fizz_, _Buzz_ or _FizzBuzz_, or an empty string. I suppose it would be more idiomatic to return a `Maybe string`, but that complicates the string concatenation and requires us to discuss more advanced topics like monads. Feel free to give it a shot and add it in the comments below. If `fizzBuzzCombo` returns an empty string show the number (`show` means `ToString()`), otherwise return a string explaining what happens with the number ("3 becomes Fizz").

Now we just need to define all our little helper functions below the `where` line. These could just as well have been full function definitions with a type annotation themselves. `fizzBuzzCombo` just concatenates `fizz` and `buzz`. Either of these could return an empty string. `fizz` and `buzz` just call a generalized function I've called `anyzz`. It takes a word, the factor and the number to test. If the number if divisible by the factor, return the word, otherwise return an empty string.

Note the definition of `fizz` (or `buzz`). You might have expected it to be `fizz n = anyzz "Fizz" 3 n`. That would also work, but when one or more arguments at the end of both sides are the same, you can just drop them. This means that `fizz` is defined to be a partial application of `anyzz` with two arguments supplied and one missing. That missing argument is the `n` that you're supposed to supply when you call `fizz`.

In order to turn this into an executable, we need a main function as well. It looks perhaps a little cryptic, like this:

```haskell
main :: IO ()
main = getArgs >>= putStrLn . fizzBuzz . read . head
```

In contrast with the `fizzBuzz` function, which is _pure_, this one is defined to return `IO ()`, which basically means it is _impure_. It has to, because it is interacting with the environment, reading indeterministic input from command line arguments and causing side effects like printing to the console output.

Let's look at `putStrLn . fizzBuzz . read . head` first. Imagine we instead had `putStrLn(fizzBuzz(read(head args)))`. `head` would take the first command line arguments and `read` would turn it into an integer. Then our `fizzBuzz` function would process it, and finally `putStrLn` would output the result to the console. `putStrLn . fizzBuzz . read . head` takes that chain of functions and replaces it with a combined function. However, we can't simply include `getArgs` in that chain, because it produces an _impure_ list of strings, and `head` requires a _pure_ list, i.e. one that is not wrapped in `IO ()`. However, the `>>=` operator is a bind operator that pulls out the actual value from the _impure_ value and calls our function chain on it.

Anyway, we can now build and test our program, using [this guide](https://wiki.haskell.org/How_to_write_a_Haskell_program):

```sh
$ cabal init
$ cabal sandbox init
$ cabal install -j
$ .cabal-sandbox/bin/FizzBuzzServer.exe 5
5 becomes Buzz
```

# The Azure Function

I have added a Function App to a Resource Group in my Azure Portal. It prompted me to also create a Storage account, which is where our queues will run. With everything set up and running, it looks like this:

![Azure Functions](https://github.com/torhovland/torhovland.github.io/raw/master/img/azure-functions.png)

If you go to "Platform features", you get all the Settings you expect from an Azure Web App. You can for example choose your deployment options. For simple prototypes I like to set up a Git repository in the Web App. Then I'll just use that for version control of the source code as well as for deployment, which is just a matter of pushing to origin. As there is no Haskell build system on Azure, this means I'm going to have to check in my Haskell executable. In a real world scenario you would probably prefer to have a build server capable of compiling your Haskell code and deploying that to Azure.

Now, if you add one of the sample PowerShell functions to your newly created Function App, configure some settings, and then clone the Git repository, you will get the initial file structure that you can work from. And when I say file structure, it's really only two files. One is a config file, `function.json`, where we will define our trigger queue and output queue:

```json
{
  "bindings": [
    {
      "name": "triggerInput",
      "type": "queueTrigger",
      "direction": "in",
      "queueName": "fizz-buzz-requests",
      "connection": "haskellstorage_STORAGE"
    },
    {
      "name": "output",
      "type": "queue",
      "direction": "out",
      "queueName": "fizz-buzz-responses",
      "connection": "haskellstorage_STORAGE"
    }
  ],
  "disabled": false
}
```

The reference to `haskellstorage_STORAGE` is something you can set up in the portal before you clone the repository:

![Azure Functions storage](https://github.com/torhovland/torhovland.github.io/raw/master/img/azure-functions-storage.png)

The other file is the PowerShell, which we edit into this:

```powershell
$in = Get-Content $triggerInput
Write-Output "PowerShell script processed queue message '$in'"
$result = D:\home\site\wwwroot\FizzBuzzHaskell\Server\.cabal-sandbox\bin\FizzBuzzServer $in
Write-Output "Haskell calculated '$result'"
Out-File -encoding Utf8 -FilePath $output -inputObject $result
```

As you can see, we refer to variables named `triggerInput` and `output`, matching the configuration file bindings.
It should really be possible to replace `D:\home\site\wwwroot` with `$env:WEBROOT_PATH`, but it seems to be blank during execution. Strange, because it's there when I check the Kudu Debug Console. Anyway, it's a static path, and not a huge issue to hardcode it. 

With this deployed, it is time to test the Function:

![Azure Functions test](https://github.com/torhovland/torhovland.github.io/raw/master/img/azure-functions-test.png)

Good, it's working!

# The client app

Of course, now we need a client that can outsource all of its Fizz Buzz processing to our shiny new Function. For that I have a small .NET Core console app that does this:

```csharp
var azureQueue = new AzureQueue(configuration["connectionStrings:haskellstorage"]);

for (int i = 1; i <= 20; i++)
{
    await azureQueue.WriteAsync("fizz-buzz-requests", i.ToString());
    Console.WriteLine($"Submitted number {i}.");
}

while (true)
{
    foreach (var message in await azureQueue.ReadAsync("fizz-buzz-responses"))
        Console.WriteLine($"Received: {message}");
}
```

`AzureQueue` is implemented like this:

```csharp
internal class AzureQueue
{
    private readonly CloudQueueClient queueClient;

    public AzureQueue(string connectionString)
    {
        queueClient = CloudStorageAccount
            .Parse(connectionString)
            .CreateCloudQueueClient();
    }

    public async Task WriteAsync(string queueName, string message)
    {
        var queue = queueClient.GetQueueReference(queueName);
        await queue.CreateIfNotExistsAsync();
        await queue.AddMessageAsync(new CloudQueueMessage(message));
    }

    public async Task<IEnumerable<string>> ReadAsync(string queueName)
    {
        var queue = queueClient.GetQueueReference(queueName);
        var cloudQueueMessages = await queue.GetMessagesAsync(10);
        return await Task.WhenAll(cloudQueueMessages.Select(m => ReadAndDeleteAsync(queue, m)));
    }

    async Task<string> ReadAndDeleteAsync(CloudQueue queue, CloudQueueMessage message)
    {
        await queue.DeleteMessageAsync(message);
        return message.AsString.Trim();
    }
}
```

Running that, we get this:

```
Submitted number 1.
Submitted number 2.
Submitted number 3.
Submitted number 4.
Submitted number 5.
Submitted number 6.
Submitted number 7.
Submitted number 8.
Submitted number 9.
Submitted number 10.
Submitted number 11.
Submitted number 12.
Submitted number 13.
Submitted number 14.
Submitted number 15.
Submitted number 16.
Submitted number 17.
Submitted number 18.
Submitted number 19.
Submitted number 20.
Received: 9 becomes Fizz
Received: 4
Received: 2
Received: 5 becomes Buzz
Received: 3 becomes Fizz
Received: 10 becomes Buzz
Received: 7
Received: 6 becomes Fizz
Received: 8
Received: 1
Received: 12 becomes Fizz
Received: 11
Received: 19
Received: 14
Received: 13
Received: 16
Received: 15 becomes FizzBuzz
Received: 18 becomes Fizz
Received: 17
Received: 20 becomes Buzz
```

The responses come out of order, just as you would expect. That's totally OK in many use cases. If you do need ordering, there are of course ways to tackle that, such as caching the responses on the client and letting it take care of ordering them.

Anyway, this should be enough for you to ace that job interview :-)
