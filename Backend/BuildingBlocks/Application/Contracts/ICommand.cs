using MediatR;

namespace Application.Contracts;

public interface ICommand : IRequest { }

public interface ICommand<out TResult> : IRequest<TResult> { }