using System.Collections.Generic;

namespace VS.SQLServerRepository
{
    public interface IBuilder<T>
    {
        List<T> Build(IDataProvider dataProvider);
    }
}