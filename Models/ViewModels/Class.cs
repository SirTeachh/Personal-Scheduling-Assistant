using System.Collections.Generic;

public class ReportsOverviewViewModel
{
    public int TotalAllocations { get; set; }
    public IEnumerable<object>? AllocationsByModule { get; set; }
    public IEnumerable<object>? AllocationsByLecturer { get; set; }
    public IEnumerable<object>? BusiestSessions { get; set; }
}
