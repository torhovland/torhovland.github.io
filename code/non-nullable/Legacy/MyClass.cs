using System;
using System.Collections;
// ReSharper disable NotAccessedField.Local
#pragma warning disable IDE0052 // Remove unread private members

namespace Legacy
{
    public class MyClass
    {
        private readonly string _requiredField;
        private readonly string _optionalField;
        private readonly SomeService _someDependency;

        public MyClass(string requiredField, string optionalField)
        {
            _requiredField = requiredField 
                ?? throw new ArgumentNullException(
                    nameof(requiredField),
                    "requiredField is required in MyClass.");

            _optionalField = optionalField;
        }

        private void DoSomething()
        {
            var l1 = _requiredField.Length;
            var l2 = _optionalField?.Length ?? 0;
            var l3 = _optionalField.Length;
        }

        private void DoSomethingWithExternalDependency()
        {
            var bar = _someDependency?.Foo?.Bar
                ?? throw new InvalidOperationException(
                    "Cannot do something, because some dependency, " +
                    "or some of its parts we require, are null.");

            bar.DoSomething();
        }
    }
}


