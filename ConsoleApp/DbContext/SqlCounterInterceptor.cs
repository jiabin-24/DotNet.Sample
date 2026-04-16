using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConsoleApp.Data;

internal sealed class SqlCounterInterceptor : DbCommandInterceptor
{
    private int _commandCount;

    public int CommandCount => _commandCount;

    public void Reset()
    {
        _commandCount = 0;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        _commandCount++;
        return base.ReaderExecuting(command, eventData, result);
    }
}
