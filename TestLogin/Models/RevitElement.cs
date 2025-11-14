namespace TestLogin.Models
{
    public class RevitElement
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string Level { get; set; }
        public double Volume { get; set; }
        public double Area { get; set; }
        public double Length { get; set; }
        public string UniqueId { get; set; }
        public string Workset { get; set; }

        public override string ToString()
        {
            return $"{Category}: {Name} (ID: {Id})";
        }
    }
}