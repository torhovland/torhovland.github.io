#pragma warning disable IDE0052 // Remove unread private members

namespace CSharpEight
{
    public class MyClass
    {
        private readonly string _requiredField;
        private readonly string? _optionalField;

        public MyClass(string requiredField, string? optionalField)
        {
            _requiredField = requiredField;
            _optionalField = optionalField;
        }


        private void DoSomething()
        {
            var l1 = _requiredField.Length;
            var l2 = _optionalField?.Length ?? 0;
            // var l3 = _optionalField.Length;
        }
    }
}


