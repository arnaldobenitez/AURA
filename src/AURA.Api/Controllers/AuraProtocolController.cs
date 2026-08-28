using AURA.Application.AuraProtocol;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Api.Controllers;

// Read-only, static protocol discovery endpoint - no database dependency, so it keeps
// answering even when SQL Server (and therefore /api/project-control) is unavailable.
// See docs/aura/PROTOCOL_VERSIONING.md for why this is the live source for CURRENT_PROTOCOL.
[ApiController]
[Route("api/aura-protocol")]
public sealed class AuraProtocolController : ControllerBase
{
    [HttpGet]
    public ActionResult<AuraProtocolMetadataDto> Get() => Ok(AuraProtocolMetadataProvider.Current);
}
