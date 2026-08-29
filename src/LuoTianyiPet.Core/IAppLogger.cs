namespace LuoTianyiPet.Core;

public interface IAppLogger
{
    void Info(string eventName, string message);

    void Error(string eventName, Exception exception);
}
