using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Text.Json;
using Shared.Dtos.Functionality;

namespace NoviBet_Api
{
    public class CommonBaseController : ControllerBase
    {
        public override OkObjectResult Ok([ActionResultObjectValue] object? value)
        {
            var res = new ApiResponse();

            if (value is null)
            {
            
                return base.Ok(res);
            }

            res.Data = JsonSerializer.Serialize(value);
            res.IsSuccess = true;
            res.ErrorMessage = null;
            return base.Ok(res);
        }
    }



   
}
