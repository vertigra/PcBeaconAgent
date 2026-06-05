using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace PcBeaconAgent.Client.Android;

public partial class App : Application
{
    private readonly MainPage mMainPage;
    public App(MainPage mainPage)
    {
        InitializeComponent();
        mMainPage = mainPage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(mMainPage);
    }
}