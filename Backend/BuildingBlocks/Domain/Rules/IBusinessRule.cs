namespace Domain.Rules;

public interface IBusinessRule
{
	string BrokenReason { get; }
	bool IsBroken();
}