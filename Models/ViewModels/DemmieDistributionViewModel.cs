namespace PersonalSchedulingAssistant.Models.ViewModels
{
    public class DemmieDistributionViewModel
    {
        public string LecturerName { get; set; } = string.Empty;
        public List<Session> Sessions { get; set; } = new();
        public List<Demmie> AvailableDemmies { get; set; } = new();
        public Dictionary<int, List<Demmie>> AssignedDemmies { get; set; } = new();
        public Dictionary<int, List<Demmie>> SessionAvailableDemmies { get; set; } = new();
    }

}
