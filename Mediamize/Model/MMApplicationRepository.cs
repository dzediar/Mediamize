//using brevo_csharp.Client;
////using brevo_csharp.Model;
using Mediamize.Api;
using Mediamize.ViewModel;
using zComp.Wpf.Model;
using zComp.Wpf.ViewModel;

namespace Mediamize.Model
{
    /// <summary>
    /// Mediamize application repository
    /// </summary>
    public class MMApplicationRepository : ApplicationRepository<MMLocalConfiguration, MMFakeApiClient>
    {
        public MMApplicationRepository() : base()
        {
            ManagedCultures[CultureFr].AddLabelsFromResource("Labels.Labels-fr-FR.txt");
            ManagedCultures[CultureEn].AddLabelsFromResource("Labels.Labels-en-US.txt");
        }
    }
}
