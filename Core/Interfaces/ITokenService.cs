using Core.Entities.Identity;

namespace Core.Interfaces
{
    public interface ITokenService
    {
        string createToken(AppUser user);
    }
}