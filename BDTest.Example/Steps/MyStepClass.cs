using System;
using System.Net.Http;
using System.Threading.Tasks;
using BDTest.Attributes;
using BDTest.Example.Context;
using BDTest.Example.Helpers;

namespace BDTest.Example.Steps
{
    public class MyStepClass
    {
        private readonly MyTestContext _context;

        public MyStepClass(MyTestContext context)
        {
            _context = context;
        }
        
        [StepText("I create a new account")]
        public async Task CreateAnAccount()
        {
            await _context.ApiContext.ExecuteRequest(new HttpRequestMessage
            {
                RequestUri = new Uri("https://www.example.com/endpoints/create"),
                Method = HttpMethod.Post,
                Content = new JsonContent<dynamic>(new
                {
                    FirstName = "Tom",
                    LastName = "Longhurst"
                })
            });
        }
    }
}