using Newtonsoft.Json;

namespace Umd.Dsa.ModoLabs.Service.Models; 

[JsonObject(MemberSerialization = MemberSerialization.Fields)]
public class UserInfo {
  public String Email {
    get;
    private set;
  } = String.Empty;

  public String FirstName {
    get;
    private set;
  } = String.Empty;

  public String ImpersonatedEmail {
    get;
    private set;
  } = String.Empty;

  public String ImpersonatedFirstName {
    get;
    private set;
  } = String.Empty;

  public String ImpersonatedLastName {
    get;
    private set;
  } = String.Empty;

  public String ImpersonatedName {
    get;
    private set;
  } = String.Empty;

  public String ImpersonatedUid {
    get;
    private set;
  } = String.Empty;

  public Boolean IsImpersonating {
    get {
      return !String.IsNullOrEmpty(ImpersonatedUid) && ImpersonatedUid != "0" && ImpersonatedUid != Uid;
    }
  }

  public String LastName {
    get;
    private set;
  } = String.Empty;

  public String Name {
    get;
    private set;
  } = String.Empty;

  public String Uid {
    get;
    private set;
  } = String.Empty;

  public Boolean Load(IReadOnlyDictionary<String, String> claims, IConfiguration configuration) {
    Email = claims["Email"] ?? String.Empty;
    Uid = claims["EmployeeID"] ?? String.Empty;
    FirstName = claims["FirstName"] ?? String.Empty;
    LastName = claims["LastName"] ?? String.Empty;
    Name = ((claims["FirstName"] ?? String.Empty) + " " + (claims["LastName"] ?? String.Empty)).Trim();

    return true;
  }

  public void LoadImpersonationDetails(ImpersonationDetails? impersonationDetails) {
    if (impersonationDetails == null) {
      ImpersonatedEmail = Email;
      ImpersonatedFirstName = FirstName;
      ImpersonatedLastName = LastName;
      ImpersonatedName = Name;
      ImpersonatedUid = Uid;
    } else {
      ImpersonatedEmail = impersonationDetails.ImpersonatedEmail;
      ImpersonatedFirstName = impersonationDetails.ImpersonatedFirstName;
      ImpersonatedLastName = impersonationDetails.ImpersonatedLastName;
      ImpersonatedName = impersonationDetails.ImpersonatedName;
      ImpersonatedUid = impersonationDetails.ImpersonatedUid;
    }
  }
}