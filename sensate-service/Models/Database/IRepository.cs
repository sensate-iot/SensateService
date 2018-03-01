/*
 * Repository interface.
 *
 * @author: Michel Megens
 * @email:  dev@bietje.net
 */

using System;
using System.Threading.Tasks;

namespace SensateService.Models.Database
{
	public interface IRepository<T> where T : class
	{
		T GetById(long id);
		bool Create(T obj);
		bool Replace(T obj1, T obj2);
		bool Update(T obj);
		bool Delete(long id);
		void Commit(T obj);
		Task CommitAsync(T obj);
	}
}
