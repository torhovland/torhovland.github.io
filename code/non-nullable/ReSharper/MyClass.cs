using System;
using System.Collections;
using JetBrains.Annotations;
// ReSharper disable NotAccessedField.Local
#pragma warning disable IDE0052 // Remove unread private members

namespace ReSharper
{
    public class MyClass
    {
        [NotNull] private readonly string _requiredField;
        [CanBeNull] private readonly string _optionalField;

        public MyClass(
            [NotNull] string requiredField, 
            [CanBeNull] string optionalField)
        {
            _requiredField = requiredField;
            _optionalField = optionalField;
        }

        private void DoSomething()
        {
            var l1 = _requiredField.Length;
            var l2 = _optionalField?.Length ?? 0;
            var l3 = _optionalField.Length;
        }
    }
}


