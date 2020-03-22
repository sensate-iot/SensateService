﻿/*
 * API key controller.
 *
 * @author Michel Megens
 * @email  michel.megens@sonatolabs.com
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using SensateService.ApiCore.Attributes;
using SensateService.ApiCore.Controllers;
using SensateService.AuthApi.Json;
using SensateService.Enums;
using SensateService.Helpers;
using SensateService.Infrastructure.Repositories;
using SensateService.Models;

namespace SensateService.AuthApi.Controllers
{
	[Produces("application/json")]
	[Route("auth/v1/[controller]")]
	[NormalUser]
	public class ApiKeysController : AbstractController
	{
		private readonly IApiKeyRepository _keys;

		public ApiKeysController(IUserRepository users, IApiKeyRepository keys, IHttpContextAccessor ctx) : base(users, ctx)
		{
			this._keys = keys;
		}

		[HttpPost("create")]
		[ActionName("CreateApiKey")]
		public async Task<IActionResult> Create([FromBody] CreateApiKey request)
		{
			SensateApiKey key = new SensateApiKey {
				Id = Guid.NewGuid().ToString(),
				UserId = this.CurrentUser.Id,
				CreatedOn = DateTime.Now.ToUniversalTime(),
				Revoked = false,
				Type = ApiKeyType.ApiKey,
				User = this.CurrentUser,
				ReadOnly = request.ReadOnly,
				Name = request.Name
			};

			await this._keys.CreateAsync(key).AwaitBackground();
			return this.CreatedAtAction("CreateApiKey", key);
		}

		private async Task<IActionResult> RevokeAll(bool systemonly)
		{
			var keys = await this._keys.GetByUserAsync(this.CurrentUser).AwaitBackground();
			IEnumerable<SensateApiKey> sorted;

			sorted = systemonly ? keys.Where(key => key.Revoked == false && key.Type == ApiKeyType.SystemKey) :
				keys.Where(key => key.Revoked == false && (key.Type == ApiKeyType.SystemKey || key.Type == ApiKeyType.ApiKey));

			await this._keys.MarkRevokedRangeAsync(sorted).AwaitBackground();
			return this.Ok();
		}

		[HttpDelete("revoke")]
		public async Task<IActionResult> Revoke([FromQuery] string id, [FromQuery] string key, [FromQuery] bool system = true)
		{
			SensateApiKey apikey;

			if(string.IsNullOrEmpty(id) && string.IsNullOrEmpty(key))
				return await this.RevokeAll(system).AwaitBackground();

			if(id != null) {
				apikey = await this._keys.GetByIdAsync(id).AwaitBackground();
			} else {
				apikey = await this._keys.GetByKeyAsync(key).AwaitBackground();
			}

			if(apikey == null) {
				return this.BadRequest();
			}

			if(apikey.Revoked) {
				return this.BadRequest();
			}

			if(apikey.UserId != this.CurrentUser.Id ||
			   !(apikey.Type == ApiKeyType.ApiKey || apikey.Type == ApiKeyType.SystemKey)) {
				return this.BadRequest();
			}

			await this._keys.MarkRevokedAsync(apikey).AwaitBackground();
			return this.Ok();
		}

		[HttpPatch("{key}")]
		[ActionName("RefreshApiKey")]
		public async Task<IActionResult> Refresh(string key)
		{
			var apikey = await this._keys.GetByIdAsync(key).AwaitBackground();

			if(apikey == null)
				return this.NotFound();

			if(!(apikey.Type == ApiKeyType.ApiKey || apikey.Type == ApiKeyType.SystemKey))
				return this.BadRequest();

			apikey = await this._keys.RefreshAsync(apikey).AwaitBackground();
			return this.CreatedAtAction("RefreshApiKey", apikey);
		}

		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var keys = await this._keys.GetByUserAsync(this.CurrentUser).AwaitBackground();
			return this.Ok(keys);
		}
	}
}
