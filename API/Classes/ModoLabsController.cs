using Microsoft.AspNetCore.Mvc;

using Umd.Dsa.ModoLabs.Service.Interfaces;
using Umd.Dsa.ModoLabs.Service.Models;

namespace Umd.Dsa.ModoLabs.Service.Classes; 

public class ModoLabsController : Controller {
  public UserInfo? CurrentUser {
    get {
      ICurrentUser? currentUser = HttpContext.RequestServices.GetService<ICurrentUser>();
      return currentUser?.Get();
    }
  }
}