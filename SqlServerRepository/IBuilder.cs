using System.Collections.Generic;

namespace ViewSource.SQLServerRepository
{
    public interface IBuilder<T>
    {
        List<T> Build(IDataProvider dataProvider);
    }
}