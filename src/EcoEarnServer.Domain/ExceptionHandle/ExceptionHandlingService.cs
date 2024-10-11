using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;

namespace EcoEarnServer.ExceptionHandle;

public class ExceptionHandlingService
{
    public static async Task<FlowBehavior> HandleException(Exception ex)
    {
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
        };
    }
}