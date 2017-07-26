using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NuGetValidators.Artifact
{
    public class AuthentiCode
    {
        private static readonly string MsftIssuerId = "Microsoft Code Signing PCA";
        private static IList<string> ResultString = new List<string>
        {
            "SUCCESS - The Certificate was successfully verified.",
            "FAILED - The Certificate is Null.",
            "FAILED - X509Certificate2.Verify() failed on the Certificate.",
            "FAILED - The Certificate is Archived.",
            "FAILED - The Certificate is not effective yet.",
            "FAILED - The Certificate has expired.",
            "FAILED - The Certificate issuer does not match Microsoft."
        };

        /// <summary>
        /// Enum to specify the result of AuthentiCode.Verify. This can be used to get a result string from  - 
        /// 0 - The Certificate was successfully verified.
        /// 1 - The Certificate is Null 
        /// 2 - X509Certificate2.Verify() failed on the Certificate 
        /// 3 - The Certificate is Archived 
        /// 4 - The Certificate is not effective yet 
        /// 5 - The Certificate has expired 
        /// 6 - The Certificate issuer does not match Microsoft
        /// </summary>
        public enum Result
        {
            Success,
            NullCertificate,
            VerifyFailed,
            Archived,
            InEffective,
            Expired,
            IssuerFailed
        }

        /// <summary>
        /// Get string equivalent of AuthentiCode.Result.
        /// </summary>
        /// <param name="result">AuthentiCode.Result to be converted into string. </param>
        /// <returns>string equivalent of result. </returns>
        public static string GetResultString(Result result)
        {
            return ResultString[(int)result];
        }

        /// <summary>
        /// Gets the certificate the file is signed with.
        /// Source https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
        /// </summary>
        /// <param name="filename">The path of the signed file from which to create the X.509 certificate. </param>
        /// <returns>The certificate the file is signed with. </returns>
        public static X509Certificate2 GetCertificate(string filename)
        {
            X509Certificate2 cert = null;
            try
            {
                cert = new X509Certificate2(filename);
            }
            catch (CryptographicException e)
            {
                Console.WriteLine($"\t\t Error {e.GetType()} : {e.Message}");
                Console.WriteLine($"\t\t Couldn’t parse the certificate.");
            }
            return cert;
        }

        /// <summary>
        /// Verifies a X509Certificate2 object for validity.
        /// </summary>
        /// <param name="cert">Certificate to be verified. </param>
        /// <returns>AuthentiCode.Result indicating the result of certificate verification. </returns>
        public static Result Verify(X509Certificate2 cert, bool displayCertMetadata)
        {
            if (cert == null)
            {
                return Result.NullCertificate;
            }

            if (displayCertMetadata)
            {
                byte[] rawdata = cert.RawData;
                Console.WriteLine($"\t\t Content Type: {X509Certificate2.GetCertContentType(rawdata)}");
                Console.WriteLine($"\t\t Certificate Verified?: {cert.Verify()}");
                Console.WriteLine($"\t\t Simple Name: {cert.GetNameInfo(X509NameType.SimpleName, true)}");
                Console.WriteLine($"\t\t Signature Algorithm: {cert.SignatureAlgorithm.FriendlyName}");
                Console.WriteLine($"\t\t Public Key: {cert.PublicKey.Key.ToXmlString(false)}");
                Console.WriteLine($"\t\t Certificate Archived?: {cert.Archived}");
                Console.WriteLine($"\t\t Length of Raw Data: {cert.RawData.Length}");
            }

            if (!cert.Verify())
            {
                return Result.VerifyFailed;
            }
            else if (cert.Archived)
            {
                return Result.Archived;
            }
            else if (DateTimeOffset.Parse(cert.GetEffectiveDateString()) > DateTimeOffset.Now)
            {
                return Result.InEffective;
            }
            else if (DateTimeOffset.Parse(cert.GetExpirationDateString()) < DateTimeOffset.Now)
            {
                return Result.Expired;
            }
            else if (!cert.GetNameInfo(X509NameType.SimpleName, true).Equals(MsftIssuerId, StringComparison.Ordinal))
            {
                return Result.IssuerFailed;
            }

            return Result.Success;
        }

        /// <summary>
        /// Extracts and Verifies a X509Certificate2 object for validity from a file.
        /// </summary>
        /// <param name="file">File for which the certificate has to be verified. </param>
        /// <returns>AuthentiCode.Result indicating the result of certificate verification. </returns>
        public static Result Verify(string file, bool displayCertMetadata)
        {
            var cert = GetCertificate(file);

            return Verify(cert, displayCertMetadata);
        }
    }
}
