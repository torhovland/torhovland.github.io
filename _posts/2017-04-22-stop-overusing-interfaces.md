---
layout: post
title: Stop overusing interfaces
subtitle: Dependency Injection using concrete classes
bigimg: /img/duplicate-content.jpg
---

[Illustration from [simpleprogrammer.com](https://simpleprogrammer.com/2012/05/27/types-of-duplication-in-code/)]

Do you use Dependency Injection? Of course you do, you're a responsible programmer, and you care about clean, maintainable, [SOLID](https://en.wikipedia.org/wiki/SOLID_(object-oriented_design)) code with low coupling. You know perfectly well that [New is Glue](http://ardalis.com/new-is-glue), and you understand the value of [programming to interfaces, not implementations](https://softwareengineering.stackexchange.com/questions/232359/understanding-programming-to-an-interface).

So do I, but please note that none of this means you should pair each and every one of your domain classes with a more or less identical interface. I suggest that you should prefer working with your concrete domain classes instead. This may sound like a dirty hack to you, but my claim is that doing this will in fact _improve_ the quality of your code. Pairing every class with an interface, on the other hand, is an [anti-pattern](https://en.wikipedia.org/wiki/Anti-pattern).

Programming to interfaces means you should try to use the most generic abstraction available to you when you program. If you have a queue of people and you need a utility method to find the tallest person, you should not do this:

```c#
public int Tallest(Queue<Person> people)
{
    return people.Max(p => p.Height);
}
```

That method does not need to care whether the collection of people is a `Queue` or something else, so you follow ReSharper's advice and do this:

```c#
public int Tallest(IEnumerable<Person> people)
{
    return people.Max(p => p.Height);
}
```

However, this does not mean that if you have a domain class `Foo` that you need to handle somewhere, you must first create the interface `IFoo` and handle that:

```c#
public void ProcessFoo(IFoo foo)
{
    ...
}
```

Perhaps you do this because you think it is dictated by SOLID, DDD or DI, but the reality is that you are in fact introducing unnecessary complexity and duplication of code, violating [YAGNI](https://en.wikipedia.org/wiki/You_aren%27t_gonna_need_it), and lowering readability and maintainability of the codebase.

So what's actually the problem? Well, the thing is that all classes already expose interfaces to begin with. The set of all non-private class members is an interface. If you decide to create an `interface IFoo` definition that matches the public members of `Foo`, you will simply have duplicated the interface of `Foo` and added no real abstraction and zero value. Browsing code like this is no fun whenever you go to a definition, expecting to get to a class implementation, and just ending up in an interface, where you need to do another step to go to the implementation.

But I need this interfaces to make Dependency Injection work, I hear you say. No, you don't. Let me demonstrate. Let's say you have a method that needs to call a domain object in order to create an order:

```c#
public async Task<IActionResult> Order()
{
    ViewBag.OrderStatus = await orderService.Order();
    return View();
}
```

Now, you don't want to do `new OrderService()` in there, and I totally agree with you on that. So you decide to pass an interface using constructor injection like this:

```c#
public HomeController(IOrderService orderService)
{
    this.orderService = orderService;
}
```

Then you just need to register your actual domain class with the container, and you're good to go:

```c#
public void ConfigureServices(IServiceCollection services)
{
    services.AddMvc();
    services.AddTransient<IOrderService, OrderService>();
}
```

Guess what? All you need to do in order to get rid of that superfluous `IOrderService` is to, well, remove it. In your constructor, receive the concrete class instead:

```c#
public HomeController(OrderService orderService)
{
    this.orderService = orderService;
}
```

As for the registration, it just gets easier:

```c#
public void ConfigureServices(IServiceCollection services)
{
    services.AddMvc();
    services.AddTransient<OrderService>();
}
```

That's it. You've now cleaned up some code. But what about unit testing? By replacing all those interfaces with concrete classes, surely I must have given up the ability to unit test my domain classes in isolation? Not at all, and I'll show you.

Imagine that the `OrderService` calls a `Storage` class to save the new order, and that both these classes make use of a `Logger` class to report progress. Let's say you want to test the `Order()` method, but without the `Storage` class talking to the database. You do, however, want to use the real `Logger`. 

The method that we want to test looks like this:

```c#
public async Task<OrderStatus> Order()
{
    logger.Log("Doing some ordering logic here...\n");
    await storage.Save();
    return OrderStatus.Ok;
}
```

The `Save()` method that we want to mock out looks like this:

```c#
public virtual async Task Save()
{
    logger.Log("Writing to a database here...\n");
    await Task.Delay(100);
}
```

If only you had used interfaces, then you could easily have mocked out the dependency to `IStorage`. Well, guess what? You can just as easily mock out a concrete class. Here's how this works using [Moq](https://github.com/moq/moq4):

```c#
[Fact]
public async void OrderReturnsOk()
{
    var logger = new Logger();
    var storageMock = new Mock<Storage>(logger);

    storageMock
        .Setup(s => s.Save())

        // Not necessary, just for illustration.
        .Callback(() => logger.Log("mocked storage"))

        .Returns(Task.CompletedTask);

    var orderService = new OrderService(logger, storageMock.Object);

    var status = await orderService.Order();

    Assert.Equal(OrderStatus.Ok, status);
    Assert.True(logger.ToString().Contains("ordering logic"));
    Assert.False(logger.ToString().Contains("database"));
    Assert.True(logger.ToString().Contains("mocked storage"));
}
```
 
So why do people keep making all those interfaces? There is quite a bit of discussion about it on the Internet, and if you're interested in more arguments in favor or against, see [here](https://softwareengineering.stackexchange.com/questions/159813/do-i-need-to-use-an-interface-when-only-one-class-will-ever-implement-it), [here](https://softwareengineering.stackexchange.com/questions/150045/what-is-the-point-of-having-every-service-class-have-an-interface) or [here](https://lostechies.com/jamesgregory/2009/05/09/entity-interface-anti-pattern/).

Please note that I'm not saying that interfaces are always bad. When they add value, they are useful. I'm only saying that interfaces that mirror one and only one class implementation is waste. Here are some cases where interfaces may be useful:

* When there is more than one implementation of a common interface. For example, if the `IStorage` interface is implemented by `DocumentDbStorage` as well as `FileStorage`.

* If you anticipate an arbitrary number of consumers that you won't have any control over, i.e. if you're making a public library such as a NuGet package, and you expect your class implementations to change more frequently than your interfaces.  

* When a single class is playing multiple roles. Then it can implement many interfaces, one for each role. This is basically the [Interface segregation principle](https://en.wikipedia.org/wiki/Interface_segregation_principle), an element of SOLID. There is an example [here](http://www.oodesign.com/interface-segregation-principle.html).

* When various unrelated classes share a common loosely interpreted behavior, such as in the `IPest` example [here](http://stackoverflow.com/a/384067/29083).

The next time you are tempted to take a single class, extract a single interface from it, and name it the same as the class with an `I` in front, think twice.

Does this make sense to you, or do you disagree? Please share your opinion in the comments below.

The sample source code used in this article is available here:
<https://github.com/torhovland/dependency-injection-without-interfaces>

> __Update 2017-04-24__
>
> Some have pointed out that making a method virtual, like I do with `Save()` above, is not without its own set of 
> maintenance issues. It's a design decision that you need to be conscious about.
>
> If you have a very high test coverage of mocking tests and you are not comfortable 
> with making your methods virtual, then sure, maybe interfaces is indeed a better option for you.
>
> The point of this article is not to say that using DI with interfaces is bad. My point is that you shouldn't 
> automatically put interfaces on everything without thinking, but certainly use them when you need them. 