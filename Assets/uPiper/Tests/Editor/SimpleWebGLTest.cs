using NUnit.Framework;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Simple WebGL test to verify Test Runner functionality
    /// </summary>
    public class SimpleWebGLTest
    {
        [Test]
        public void WebGL_Simple_Test_Works()
        {
            // This test should always appear in Test Runner
            Assert.IsTrue(true, "Simple WebGL test should pass");
        }
        
        [Test]
        public void WebGL_Platform_Independent_Test()
        {
            // This test verifies basic functionality
            string testString = "WebGL";
            Assert.IsNotNull(testString);
            Assert.AreEqual("WebGL", testString);
        }
    }
}