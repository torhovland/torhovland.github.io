using System.Threading;

namespace Legacy
{
    internal class SomeService
    {
        public Foo Foo { get; private set; }
    }

    internal class Foo
    {
        public Bar Bar { get; set; }
    }

    internal class Bar
    {
        public void DoSomething()
        {
            throw new System.NotImplementedException();
        }
    }
}