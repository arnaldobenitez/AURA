namespace AURA.Domain.ProjectControl;

public enum ProjectEventType
{
    Claim = 1,
    Progress = 2,
    Handoff = 3,
    Blocker = 4,
    Completion = 5,
    Request = 6,
    Approval = 7,
    Rework = 8,
    Deployment = 9,
}
