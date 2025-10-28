namespace PersonalSchedulingAssistant.Models.ViewModels
{
    public class AllocationPreviewStudent
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = "";
        public string VenueName { get; set; } = "";
    }

    public class AllocationConfigViewModel
    {
        // Inputs
        public int? SelectedModuleId { get; set; }
        public List<int> SelectedVenueIds { get; set; } = new();
        public string AllocationType { get; set; } = "First Come, First Serve";
        public int GroupSizeLimit { get; set; }
        public int TotalStudents { get; set; }

        // For dropdowns
        public IEnumerable<Module>? Modules { get; set; } = new List<Module>();
        public IEnumerable<Venue>? Venues { get; set; } = new List<Venue>();
        public List<Session>? Sessions { get; set; } = new();

        public int? SelectedSessionId { get; set; }
        public string? SelectedSessionName { get; set; }


        // Preview
        public Dictionary<string, List<AllocationPreviewStudent>> Preview { get; set; }
        = new Dictionary<string, List<AllocationPreviewStudent>>();
    }

}
