namespace Domain.Events;

public interface IDomainEventEmitter
{
	public IReadOnlyList<IDomainEvent> DomainEvents { get; }
	public void AddDomainEvent(IDomainEvent domainEvent);
	public void RemoveDomainEvent(IDomainEvent domainEvent);
	public void RemoveAllDomainEvents();
}