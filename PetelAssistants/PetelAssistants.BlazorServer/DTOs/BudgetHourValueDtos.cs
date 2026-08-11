namespace PetelAssistants.BlazorServer.DTOs
{
    public class BudgetHourValueDto
    {
        public int Id { get; set; }
        public int HebrewYearId { get; set; }
        public decimal HourValue { get; set; }
    }

    public class UpsertBudgetHourValueRequest
    {
        public int HebrewYearId { get; set; }
        public decimal HourValue { get; set; }
    }
}
