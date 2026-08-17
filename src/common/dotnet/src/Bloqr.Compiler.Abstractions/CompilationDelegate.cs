namespace Bloqr.Compiler.Abstractions;

/// <summary>
/// Delegate for the next middleware in the pipeline.
/// </summary>
/// <param name="context">The compilation context.</param>
public delegate Task CompilationDelegate(CompilationContext context);