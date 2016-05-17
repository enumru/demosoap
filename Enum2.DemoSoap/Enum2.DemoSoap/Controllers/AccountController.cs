using System;
using System.Configuration;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Enum2.DemoSoap.Models;
using Enum2.DemoSoap.ServiceReference1;

namespace Enum2.DemoSoap.Controllers
{
    public class AccountController : Controller
    {
        private const string AuthServerBaseUrl = "https://auth.enum.ru";
        private readonly string _baseUrl;
        private readonly string _consumerId;
        private readonly string _consumerSecret;

        public AccountController()
        {
            _baseUrl = ConfigurationManager.AppSettings["BaseUrl"];
            _consumerId = ConfigurationManager.AppSettings["ConsumerId"];
            _consumerSecret = ConfigurationManager.AppSettings["ConsumerSecret"];
        }

        public ActionResult Login(string returnUrl)
        {
            var model = new LoginForm {Email = Session["email"] as string};

            var respErr = TempData["responseError"] as string;
            if (respErr != null) ModelState.AddModelError("Email", respErr);

            return View("Login", model);
        }

        [HttpPost]
        public ActionResult Login(LoginForm model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                Session["email"] = model.Email;

                var email = model.Email;
                var userIp = Request.UserHostAddress;
                var crc = Sha256(_consumerSecret + email);

                var client = new SoapSoapClient();
                var result = client.GetChallenge(_consumerId, email, userIp, crc);

                if (result.ErrorCode == 0)
                {
                    Session["challenge"] = result.Challenge;
                    Session["qrUrl"] = result.QrUrl;
                    return RedirectToAction("Login2", new {ReturnUrl = returnUrl});
                }

                if (result.ErrorCode == 301)
                    return Redirect(AskPermissionUrl(email, returnUrl));


                var errorMsg = client.GetErrDesc(result.ErrorCode, "en");
                ModelState.AddModelError("Email", errorMsg);
            }
            return View("Login", model);
        }

        private string AskPermissionUrl(string email, string returnUrl)
        {
            var nonce = Sha256("" + new Random().Next(10000, 99999));
            Session["nonce"] = nonce;

            var redirectUrl = _baseUrl + "/Account/EnumCallback";
            redirectUrl = UrlWithGetParam(redirectUrl, "ReturnUrl", returnUrl);

            var url = AuthServerBaseUrl + "/Permission/Ask";
            url = UrlWithGetParam(url, "ConsumerId", _consumerId);
            url = UrlWithGetParam(url, "Email", email);
            url = UrlWithGetParam(url, "RedirectUrl", redirectUrl);
            url = UrlWithGetParam(url, "Nonce", nonce);

            return url;
        }

        public ActionResult Login2(string returnUrl)
        {
            var email = Session["email"] as string;
            if (string.IsNullOrWhiteSpace(email))
                return RedirectToAction("Login", new {ReturnUrl = returnUrl});

            var model = new Login2Form
            {
                Email = email,
                Challenge = Session["challenge"] as string,
                QrUrl = Session["qrUrl"] as string,
            };

            var respErr = TempData["responseError"] as string;
            if (respErr != null) ModelState.AddModelError("Response", respErr);

            return View("Login2", model);
        }

        [HttpPost]
        public ActionResult Login2(Login2Form model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                var userIp = Request.UserHostAddress;
                var crc = Sha256(_consumerSecret + model.Response);

                var client = new SoapSoapClient();
                var errorCode = client.CheckUserAnswer(_consumerId, model.Email, model.Challenge, model.Response, "", userIp, crc);
                if (errorCode == 0)
                {
                    FormsAuthentication.RedirectFromLoginPage(model.Email, false);
                    return new EmptyResult();
                }


                var errorMsg = client.GetErrDesc(errorCode, "en");
                TempData["responseError"] = errorMsg;

                var email = model.Email;
                crc = Sha256(_consumerSecret + email);
                var result = client.GetChallenge(_consumerId, email, userIp, crc);
                if (result.ErrorCode == 0)
                {
                    Session["challenge"] = result.Challenge;
                    Session["qrUrl"] = result.QrUrl;
                    return RedirectToAction("Login2", new {ReturnUrl = returnUrl});
                }

                if (result.ErrorCode == 301)
                {
                    TempData["responseError"] = null;
                    return Redirect(AskPermissionUrl(email, returnUrl));
                }

                Session["challenge"] = Session["qrUrl"] = null;
                return RedirectToAction("Login", new {ReturnUrl = returnUrl});
            }

            if (string.IsNullOrWhiteSpace(model.Email))
                return RedirectToAction("Login", new {ReturnUrl = returnUrl});

            return View("Login2", model);
        }

        public ActionResult EnumCallback(string email, string crc, string nonce, string allow, string deny, string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(email))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "email required");

            if (string.IsNullOrWhiteSpace(crc))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "crc required");

            var hash = Sha256(_consumerSecret + email);
            if (crc != hash)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "crc invalid");

            if (!string.IsNullOrWhiteSpace(deny) || allow != "1")
                return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace((string) Session["nonce"]) || nonce != (string) Session["nonce"])
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "nonce invalid");

            Session.Remove("nonce");

            FormsAuthentication.RedirectFromLoginPage(email, false);
            return View();
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Home");
        }

        private static string Sha256(string s)
        {
            var dataToHash = new UTF8Encoding().GetBytes(s);
            var hashValue = ((HashAlgorithm) CryptoConfig.CreateFromName("SHA256")).ComputeHash(dataToHash);
            var hashStr = BitConverter.ToString(hashValue);
            return hashStr.Replace("-", "").ToLower(); // without dashes and lowercase
        }

        private static string UrlWithGetParam(string uriString, string getParamName, string getParamValue)
        {
            var uri = new UriBuilder(uriString);
            var p = HttpUtility.ParseQueryString(uri.Query);
            p[getParamName] = getParamValue;
            uri.Query = p.ToString();
            return uri.ToString();
        }
    }
}