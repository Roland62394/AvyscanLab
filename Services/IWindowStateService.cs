namespace AvyscanLab.Services;

public interface IWindowStateService
{
    WindowSettings? Load();
    void Save(WindowSettings settings);
}
