namespace EasyQuery;

/// <summary>
///     Initialization methods for EasyQuery.
/// </summary>
public static class EasyQuery
{
    /// <summary>
    ///     Loads embedded dependencies.
    ///     This method should be called early in your application lifecycle, before creating any
    ///     <see cref="EasyQueryClient" /> instances or calling their methods.
    /// </summary>
    /// <remarks>
    ///     This method should be called from a different method than where you instantiate or use
    ///     <see cref="EasyQueryClient" /> to ensure proper assembly loading. Typically, call this
    ///     from your application's entry point (e.g., Main method) or during application startup.
    /// </remarks>
    /// <example>
    ///     <code>
    /// static void Main(string[] args)
    /// {
    ///     EasyQuery.Prepare();
    ///     
    ///     // Your application logic here...
    ///     RunMyApplication();
    /// }
    /// 
    /// static void RunMyApplication()
    /// {
    ///     using var client = new EasyQueryClient("127.0.0.1", 7777, "password");
    ///     // Use client...
    /// }
    /// </code>
    /// </example>
    public static void Prepare()
    {
        CosturaUtility.Initialize();
    }
}