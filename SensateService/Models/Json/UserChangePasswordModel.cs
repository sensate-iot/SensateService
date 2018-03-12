/*
 * Forgot password JSON model.
 *
 * @author Michel Megens
 * @email   dev@bietje.net
 */

namespace SensateService.Models.Json
{
	public class UserChangePasswordModel
	{
		public string Email {get;set;}
		public string Password {get;set;}
		public string Token {get;set;}
	}
}
