namespace Logibooks.Core.Interfaces;

using Logibooks.Core.RestModels;

public interface IProgressReporter
{
    ValidationProgress? GetProgress(Guid handleId);
    bool Cancel(Guid handleId);
}

