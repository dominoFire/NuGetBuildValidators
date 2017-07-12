using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGetValidators.Artifact
{
    public class AuthentiCode
    {

        /// <summary>
        /// Gets the certificate the file is signed with.
        /// Source https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
        /// </summary>
        /// <param name=”filename”>The path of the signed file from which to create the X.509 certificate. </param>
        /// <returns>The certificate the file is signed with</returns>
        public X509Certificate GetCertificate(string filename)
        {
            X509Certificate cert = null;
            try
            {
                cert = X509Certificate.CreateFromSignedFile(filename);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine("Error { 0} : { 1}", e.GetType(), e.Message);
                Console.WriteLine("Couldn’t parse the certificate.");
            }
            return cert;
        }
    }
}
