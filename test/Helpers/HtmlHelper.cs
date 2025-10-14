using Microsoft.AspNetCore.Mvc.Rendering;

namespace test.Helpers
{
    public static class HtmlHelper
    {
        public static string ActiveClass(ViewContext viewContext, string controller, string action)
        {
            var currentController = viewContext.RouteData.Values["controller"]?.ToString();
            var currentAction = viewContext.RouteData.Values["action"]?.ToString();
            return (controller == currentController && action == currentAction) ? "active" : "";
        }
    }
}
