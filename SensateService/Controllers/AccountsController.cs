/*
 * RESTful account controller.
 *
 * @author: Michel Megens
 * @email:  dev@bietje.net
 */

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Diagnostics;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using SensateService.Helpers;
using SensateService.Infrastructure.Repositories;
using SensateService.Models;
using SensateService.Models.Json;
using SensateService.Services;

namespace SensateService.Controllers
{
	[Route("v{version:apiVersion}/[controller]")]
	[ApiVersion("1")]
	public class AccountsController : Controller
	{
		private readonly UserAccountSettings _settings;
		private readonly SignInManager<SensateUser> _siManager;
		private readonly UserManager<SensateUser> _manager;
		private readonly IUserRepository _users;
		private readonly IEmailSender _mailer;
		private readonly IPasswordResetTokenRepository _tokens;

		public AccountsController(
			IUserRepository repo,
			SignInManager<SensateUser> manager,
			UserManager<SensateUser> userManager,
			IOptions<UserAccountSettings> options,
			IEmailSender emailer,
			IPasswordResetTokenRepository tokens
		)
		{
			this._users = repo;
			this._siManager = manager;
			this._settings = options.Value;
			this._manager = userManager;
			this._mailer = emailer;
			this._tokens = tokens;
		}

		[HttpPost("forgot-password")]
		public async Task<IActionResult> ForgotPassword([FromBody] UserForgotPasswordModel model)
		{
			SensateUser user;
			string usertoken;

			user = await this._users.GetByEmailAsync(model.Email);
			if(user == null || !user.EmailConfirmed)
				return NotFound();

			var token = await this._manager.GeneratePasswordResetTokenAsync(user);
			token = Base64UrlEncoder.Encode(token);
			usertoken = this._tokens.Create(token);

			if(usertoken == null)
				return this.StatusCode(500);

			Debug.WriteLine($"Password reset URL: {usertoken}");
			return Ok();
		}

		[HttpPost("reset-password")]
		public async Task<IActionResult> Resetpassword([FromBody] UserChangePasswordModel model)
		{
			SensateUser user;
			PasswordResetToken token;

			if(model.Email == null || model.Password == null || model.Token == null)
				return BadRequest();

			user = await this._users.GetByEmailAsync(model.Email);
			token = this._tokens.GetById(model.Token);

			if(user == null || token == null)
				return NotFound();

			token.IdentityToken = Base64UrlEncoder.Decode(token.IdentityToken);
			var result = await this._manager.ResetPasswordAsync(user, token.IdentityToken, model.Password);

			if(result.Succeeded)
				return Ok();

			return new NotFoundObjectResult(new {Message = result.Errors});
		}

		[HttpPost("login")]
		public async Task<object> Login([FromBody] LoginModel loginModel)
		{
			var result = await this._siManager.PasswordSignInAsync(
				loginModel.Email,
				loginModel.Password,
				false,
				false
			);

			if(result.Succeeded) {
				var user = await this._users.GetByEmailAsync(loginModel.Email);
				return this.GenerateJwtToken(loginModel.Email, user);
			}

			return NotFound();
		}

		private bool ValidateUser(SensateUser user)
		{
			if(user.FirstName == null || user.FirstName.Length == 0)
				return false;

			if(user.LastName == null || user.LastName.Length == 0)
				return false;

			return true;
		}

		[HttpPost("register")]
		public async Task<object> Register([FromBody] RegisterModel register)
		{
			var user = new SensateUser {
				UserName = register.Email,
				Email = register.Email,
				FirstName = register.FirstName,
				LastName = register.LastName,
				PhoneNumber = register.PhoneNumber
			};

			if(!this.ValidateUser(user))
				return BadRequest();

			var result = await this._manager.CreateAsync(user, register.Password);

			if(result.Succeeded) {
				user = await this._users.GetAsync(user.Id);
				var code = await this._manager.GenerateEmailConfirmationTokenAsync(user);
				code = Base64UrlEncoder.Encode(code);
				var url = Url.EmailConfirmationLink(user.Id, code, Request.Scheme);
				Debug.WriteLine($"Confirmation URL: {url}");
				await this._mailer.SendEmailAsync(user.Email, "Confirm email!", url);
				return Ok();
			}

			return BadRequest();
		}

		[HttpGet("show")]
		[Authorize]
		public async Task<IActionResult> Show()
		{
			dynamic jobj;
			var user = await this._users.GetCurrentUserAsync(this.User);

			if(user == null)
				return NotFound();

			jobj = new JObject();

			jobj.FirstName = user.FirstName;
			jobj.LastName = user.LastName;
			jobj.Email = user.Email;
			jobj.PhoneNumber = user.PhoneNumber ?? "";

			return new ObjectResult(jobj);
		}

		[HttpGet("confirm/{id}/{code}")]
		public async Task<IActionResult> ConfirmEmail(string id, string code)
		{
			SensateUser user;

			if(id == null || code == null) {
				return BadRequest();
			}

			user = await this._users.GetAsync(id);
			if(user == null)
				return NotFound();

			/*
			 * For some moronic reason we need to encode and decode to
			 * Base64. The + sign gets * mangled to a ' ' if we don't.
			 */
			code = Base64UrlEncoder.Decode(code);
			var result = await this._manager.ConfirmEmailAsync(user, code);
			if(!result.Succeeded)
				return Unauthorized();

			return this.Ok();
		}

		private object GenerateJwtToken(string email, SensateUser user)
		{
			List<Claim> claims;
			JwtSecurityToken token;

			claims = new List<Claim> {
				new Claim(JwtRegisteredClaimNames.Sub, email),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
				new Claim(ClaimTypes.NameIdentifier, user.Id)
			};

			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this._settings.JwtKey));
			var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
			var expires = DateTime.Now.AddDays(this._settings.JwtExpireDays);
			token = new JwtSecurityToken(
				issuer: this._settings.JwtIssuer,
				audience: this._settings.JwtIssuer,
				claims: claims,
				expires: expires,
				signingCredentials: creds
			);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}
	}
}
