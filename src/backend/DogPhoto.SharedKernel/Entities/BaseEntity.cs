namespace DogPhoto.SharedKernel.Entities;

public abstract class BaseEntity<TId> where TId : notnull
{
    public TId Id { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted => DeletedAt.HasValue;

    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public void SoftDelete() => DeletedAt = DateTime.UtcNow;

    public void Restore() => DeletedAt = null;
}

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
