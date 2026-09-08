using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace TeaLearning.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected Guid? GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                      ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(idClaim, out var id))
        {
            return id;
        }

        return null;
    }

    protected Guid? GetCurrentStudentId()
    {
        var studentIdClaim = User.FindFirst("estudianteId")?.Value;
        if (Guid.TryParse(studentIdClaim, out var id))
        {
            return id;
        }

        return null;
    }
}
