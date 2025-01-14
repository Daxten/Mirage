using NUnit.Framework;

namespace Mirage.Tests
{
    [TestFixture]
    public class DeserializeFailedExceptionTest
    {
        [Test]
        public void InvalidMessageWithTextTest()
        {
            DeserializeFailedException ex = Assert.Throws<DeserializeFailedException>(() =>
            {
                throw new DeserializeFailedException("Test Message");
            });

            Assert.That(ex.Message, Is.EqualTo("Test Message"));
        }
    }
}
