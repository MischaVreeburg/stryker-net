using System.Diagnostics;

namespace Stryker.Core.Reporters.HtmlReporter.ProcessWrapper
{
    public interface IWebbrowserOpener
    {
        /// <summary>
        /// Opens a file or url in the OS default program
        /// </summary>
        /// <param name="path">The path to the file or url</param>
        Process Open(string path);
    }
}
