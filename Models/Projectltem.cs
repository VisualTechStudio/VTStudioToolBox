namespace VTStudioToolBox.Models
{
    public class ProjectItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;

        public ProjectItem() { }

        public ProjectItem(string name, string path)
        {
            Name = name;
            Path = path;
        }
    }
}