

using System.Collections;

namespace Ck.Database
{
    public interface ICollectionBase
    {
        void Add(object item);
        void Remove(object item);
        void RemoveById(int id);
        object FindById(int id);
        IEnumerable GetAll();
        void Load();
        void Save();
    }
}
