/*
 * User repository interface.
 *
 * @author: Michel Megens
 * @email:  dev@bietje.net
 */

using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;

using SensateService.Models;

namespace SensateService.Infrastructure.Repositories
{
	public interface IUserRepository
	{
		SensateUser Get(string key);
		Task<SensateUser> GetAsync(string key);
		SensateUser GetByEmail(string key);
		Task<SensateUser> GetByEmailAsync(string key);

		SensateUser GetCurrentUser(ClaimsPrincipal cp);
		Task<SensateUser> GetCurrentUserAsync(ClaimsPrincipal cp);

		void StartUpdate(SensateUser user);
		Task EndUpdateAsync();
	}
}
