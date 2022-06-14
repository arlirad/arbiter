using System;

namespace Arbiter {
    [Identifier("mime")]
    public class MimeStatement : IStatement {
        public void Read(TokenStream stream) {
            string ext = stream.ExpectString();
            string mime = stream.ExpectString();
            
            Server.Handler.Mime[ext] = mime;
        }
    }
}