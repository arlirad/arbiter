using System;

namespace Arbiter;

public interface IRewriter
{
    void Rewrite(Request request);
}