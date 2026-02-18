namespace PetelApp.BlazorServer.DTOs
{
    public class TransactionDto
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public int TransactionTypeId { get; set; }
        public string TransactionTypeName { get; set; } = string.Empty;
        public string TransactionTypeDescription { get; set; } = string.Empty;
        public bool IsCredit { get; set; }
        public DateTime TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? RelatedTransactionId { get; set; }
        public int? RelatedStudentId { get; set; }
        public string? RelatedStudentName { get; set; }
        public int? SchoolYearId { get; set; }
        public string? SchoolYearName { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class TransactionDetailDto
    {
        public int Id { get; set; }
        public int TransactionId { get; set; }
        public int DetailTypeId { get; set; }
        public string DetailTypeName { get; set; } = string.Empty;
        public string DetailTypeDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? RelatedStudentId { get; set; }
        public string? RelatedStudentName { get; set; }
    }

    public class TransactionTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsCredit { get; set; }
        public bool IsActive { get; set; }
    }

    public class TransactionDetailTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CreateTransactionRequest
    {
        public int AccountId { get; set; }
        public int TransactionTypeId { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.Today;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? RelatedTransactionId { get; set; }
        public int? RelatedStudentId { get; set; }
        public int? SchoolYearId { get; set; }
        public List<CreateTransactionDetailRequest> Details { get; set; } = new();
    }

    public class CreateTransactionDetailRequest
    {
        public int DetailTypeId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class TransactionFilterRequest
    {
        public int AccountId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? TransactionTypeId { get; set; }
        public int? SchoolYearId { get; set; }
        public int? RelatedStudentId { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
    }

    public class TransactionWithDetailsDto
    {
        public TransactionDto Transaction { get; set; } = new();
        public List<TransactionDetailDto> Details { get; set; } = new();
    }
}
