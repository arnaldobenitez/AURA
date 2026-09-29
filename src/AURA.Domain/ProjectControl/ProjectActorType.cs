namespace AURA.Domain.ProjectControl;

public enum ProjectActorType
{
    Unassigned = 0,
    Human = 1,
    AiAnalyst = 2,
    Coder = 3,
    // Technical ingestion channel for AURA Agent Registration Protocol v2.3. It may write
    // append-only registration events, but it is not a governed actor and must not acquire
    // leases, change control, administer Roadmaps, or approve stages.
    Ingestion = 4,
}
