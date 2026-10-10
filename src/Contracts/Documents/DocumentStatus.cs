namespace Contracts.Documents;

public enum DocumentStatus
{
    Succeeded,

    /// <summary>This document could not be analysed; the job itself still continues.</summary>
    Failed
}
