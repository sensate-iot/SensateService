﻿/*
 * AuditLog repository implementation.
 *
 * @author Michel Megens
 * @email  dev@bietje.net
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;

using Microsoft.EntityFrameworkCore;

using SensateService.Infrastructure.Repositories;
using SensateService.Models;
using SensateService.Enums;
using SensateService.Helpers;

namespace SensateService.Infrastructure.Sql
{
	public class AuditLogRepository : AbstractSqlRepository<AuditLog>, IAuditLogRepository
	{
		public AuditLogRepository(SensateSqlContext ctx) : base(ctx)
		{
		}

		public void Create(string route, RequestMethod method, IPAddress address, SensateUser user = null)
		{
			var al = new AuditLog() {
				AuthorId = user?.Id,
				Route = route,
				Method = method,
				Address = address,
				Timestamp = DateTime.Now,
				Id = 0L
			};

			this.Create(al);
		}

		public async Task CreateAsync(string route, RequestMethod method, IPAddress address, SensateUser user = null)
		{
			AuditLog al;

			al = new AuditLog() {
				AuthorId = user?.Id,
				Route = route,
				Method = method,
				Address = address,
				Timestamp = DateTime.Now,
				Id = 0L
			};

			await this.CreateAsync(al).AwaitBackground();
		}

		public async Task CreateAsync(AuditLog obj, CancellationToken ct)
		{
			this.Data.Add(obj);
			await this.CommitAsync(ct).AwaitBackground();
		}

		public async Task<IEnumerable<AuditLog>> GetByRequestType(SensateUser user, RequestMethod method)
		{
			var query = from log in this.Data
				where log.Method == method && log.Author == user
				select log;
			return await query.ToListAsync().AwaitBackground();
		}

		public AuditLog Get(long id)
		{
			AuditLog al;

			al = (from log in this.Data.AsQueryable()
				  where log.Id == id
				  select log).Single();
			return al;
		}

		public async Task<AuditLog> GetAsync(long id)
		{
			return await Task.Run(() => this.Get(id)).AwaitBackground();
		}

		public IEnumerable<AuditLog> GetBetween(SensateUser user, DateTime start, DateTime end)
		{
			var query = from log in this.Data
				where log.Author == user && log.Timestamp >= start && log.Timestamp <= end
				select log;
			return query.ToList();
		}

		public async Task<IEnumerable<AuditLog>> GetBetweenAsync(SensateUser user, DateTime start, DateTime end)
		{
			var query = from log in this.Data
				where user == log.Author && log.Timestamp >= start && log.Timestamp <= end
				select log;
			return await query.ToListAsync().AwaitBackground();
		}

		public AuditLog GetById(long id)
		{
			return this.Get(id);
		}

		public IEnumerable<AuditLog> GetByRoute(SensateUser user, string route)
		{
			var al = from log in this.Data
					 where log.Route == route && user == log.Author
					 select log;
			return al.ToList();
		}

		public async Task<int> CountAsync(Expression<Func<AuditLog, bool>> predicate)
		{
			return await this.Data.Select(predicate).CountAsync().AwaitBackground();
		}

		public async Task<IEnumerable<AuditLog>> GetByRouteAsync(SensateUser user, string route)
		{
			return await Task.Run(() => this.GetByRoute(user, route)).AwaitBackground();
		}

		public IEnumerable<AuditLog> GetByUser(SensateUser user)
		{
			var al = from log in this.Data.AsQueryable()
					 where log.Author == user
					 select log;
			return al;
		}

		public async Task<IEnumerable<AuditLog>> GetByUserAsync(SensateUser user)
		{
			return await Task.Run(() => this.GetByUser(user)).AwaitBackground();
		}
	}
}
