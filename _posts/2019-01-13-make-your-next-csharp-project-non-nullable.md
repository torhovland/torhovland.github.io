# Make your next C# project non-nullable

I'm sure you're well aware how the `null` value is responsible for loads of extra, uninspiring code, and tons and tons of exceptions and crashes. Tony Hoare came up with this "innovation" in 1965, and he has later apologized for what he calls a [_billion dollar mistake_](https://en.wikipedia.org/wiki/Null_pointer#History). 

Here's an example of the extra null checking we need to do. What we really want is that `requiredField` can never be `null`, but we have no way to make the compiler enforce that. Hence, we need to do the checking ourselves.

![](legacy-myclass.png)

You might find it pointless to simply substitute one exception (`NullReferenceException`) with another (`ArgumentNullException`) like that, but it really isn't. A `NullReferenceException` can emerge from deep within your code, without any hint about what shouldn't have been `null`, forcing you to sit down and inspect or debug the code in order to find out what's wrong. If you're lucky you have symbol files with a reference to the line number where the exception was thrown, but in production code the line numbers are often wrong due to optimized code, or the symbol files are missing altogether. An `ArgumentNullException` is much more helpful, because it will refer to the problematic argument and hopefully also provide a meaningful error message.

Unfortunately, it's often not possible to know whether or not a reference can be null. That leaves us with the choice of sprinkling `null` checks all over the code, or just make vague assumptions that some references will never be `null`. Here's an example of the former: 

![](excessive-null-checking.png)

As professionals, perhaps we're expected to check all code like this? Fine, but in many cases this extra code actually _is_ superfluous. It is quite possible that `_someDependency`, `Foo` and `Bar` can never be null. But we're not getting any hints from the compiler about that. And even if we inspect the code manually and conclude that this is the case, things may change in the future if `SomeDependency` is modified.

What a mess this is! I'm surprised developers have accepted this state of affairs for so long without rioting! It's easy to see why this is called a _billion dollar mistake_. In fact, that's likely to be an understatement.

Sadly, most programming languages have inherited `null`, but languages like Haskell, OCaml, Scala, F#, Elm, and Rust have chosen to make use of [_option types_](https://en.wikipedia.org/wiki/Option_type) as a robust alternative. 

Option types is an elegant solution as long as it has been designed into the language to begin with. Retrofitting it into languages with `null` is problematic, because now there are two ways to represent missing vales. You will still have to check for `null` when dealing with legacy code. Nevertheless, this is the approach taken with Java 8 and the new `Optional` type.

In contrast, Kotlin has managed to embrace `null` as part of the language in a way that is both safe and pragmatic. How is that possible? The solution is actually quite straightforward when you focus on what we actually want to achieve: that it should not be possible to dereference a `null` reference. All right, so all references need to explicitly be one of the following:

- Non-nullable
- Nullable, and thus not possible to dereference without a null check

In a way, this has been available to C# developers for [ten years](https://blogs.msmvps.com/peterritchie/2008/07/21/working-with-resharper-s-external-annotation-xml-files/) already, using the [ReSharper Annotation Framework](https://www.jetbrains.com/resharper/features/code_analysis.html). 

By annotating the code with attributes, ReSharper can help us where the C# type system falls short:

![](resharper-myclass.png)

If I now try to set `requiredField` to `null`, ReSharper will catch it:

![](resharper-required-not-null.png)

I will get a similar warning if I try to dereference `optionalField` without checking for null:

![](resharper-null-reference.png)

This is really helpful, but far from perfect. First, these are just helpful hints and not compile errors. Second, they rely on a third-party tool.  Third, you're not forced to annotate all the code, so the problem never really goes away. And ReSharper defaults to an _optimistic_ analysis, meaning that when annotations are missing, avoiding false alarms are deemed more important than pointing out all potential code issues. Fourth, all these annotations aren't exactly making the code any prettier.

Luckily, we no longer have to concern ourselves with any of that, because C# 8 comes with _nullable reference types_ built-in. The naming is a bit confusing, because reference types have always been nullable, and that's the whole problem. The novelty is that they can now also be _non-nullable_. The reason for the name is that reference types can now be _non-nullable_ by default, or explicitly _nullable_ using `Nullable<T>` or `T?`. For backward compatibility, you need to enable the feature in the code or project files. Also, any issues detected by the compiler are just warnings by default, so you may want to treat warnings as errors.

Here's the same class as before, but note how much cleaner this looks without the extra attributes:

![](csharp-myclass.png)

If I try to set `requiredField` to `null`, I get a similar message as before, but this time from the compiler itself:

![](csharp-required-not-null.png)

Naturally, it also catches a dereferencing of `optionalField`:

![](csharp-null-reference.png)

Needless to say, I think we should all enable this feature for any new code[^interop], using the following project file settings:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>8.0</LangVersion>
    <NullableReferenceTypes>true</NullableReferenceTypes>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

</Project>
```

How about existing code? Should we go ahead and enable nullable reference types there too? Not necessarily, because going through all the compiler errors and fixing them is a significant job, and there is always the risk that this in itself will introduce new bugs. If you are motivated to try, see [here](https://praeclarum.org/2018/12/17/nullable-reference-types.html) and [here](https://codeblog.jonskeet.uk/2018/04/21/first-steps-with-nullable-reference-types/) for a taste of what to expect.

[^interop]: This is under the assumption that you're working on application code where you can enable C# 8 everywhere. If you're building a library and expect some clients to use older versions of C#, or to simply ignore the new compile warnings, you need to consider adding the extra `null` checking as before. More about this [here](https://csharp.christiannagel.com/2018/06/20/nonnullablereferencetypes/).
