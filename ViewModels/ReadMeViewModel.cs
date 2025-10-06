using System;
using System.IO;

namespace EconToolbox.Desktop.ViewModels
{
    public class ReadMeViewModel : BaseViewModel
    {
        private string _markdownContent = "Loading README...";

        public string MarkdownContent
        {
            get => _markdownContent;
            private set => SetProperty(ref _markdownContent, value);
        }

        public ReadMeViewModel()
        {
            LoadReadMe();
        }

        private void LoadReadMe()
        {
            try
            {
                var baseDirectory = AppContext.BaseDirectory;
                var candidatePaths = new[]
                {
                    Path.Combine(baseDirectory, "README.md"),
                    Path.Combine(baseDirectory, "..", "..", "..", "README.md")
                };

                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        MarkdownContent = File.ReadAllText(path);
                        return;
                    }
                }

                MarkdownContent = "README.md could not be located in the application directory.";
            }
            catch (Exception ex)
            {
                MarkdownContent = $"Unable to load README.md: {ex.Message}";
            }
        }
    }
}
