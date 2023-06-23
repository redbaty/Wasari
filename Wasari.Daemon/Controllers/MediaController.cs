using Microsoft.AspNetCore.Mvc;
using Wasari.Daemon.Models;
using Wolverine;

namespace Wasari.Daemon.Controllers;

[ApiController]
[Route("[controller]")]
public class MediaController : ControllerBase
{
    public MediaController(IMessageBus bus)
    {
        Bus = bus;
    }

    private IMessageBus Bus { get; }
    
    [HttpPost("download")]
    public async Task<IActionResult> Download([FromBody] DownloadRequest request)
    {
        if (!request.Url.IsAbsoluteUri)
            return BadRequest();
        
        await Bus.SendAsync(request);
        return Accepted();
    }
}