using ManagedRunspacePool2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class AppdomainTests1
    {
        [TestMethod]
        public void TestCreating_Appdomain()
        {
            Debug.WriteLine("Creating new AppDomain.");
            AppDomain domain = AppDomain.CreateDomain("MyDomain");

            Debug.WriteLine("Host domain: " + AppDomain.CurrentDomain.FriendlyName);
            Debug.WriteLine("child domain: " + domain.FriendlyName);
        }

        [TestMethod]
        public void Unload_Appdomain()
        {
            AppDomain domain = AppDomain.CreateDomain("MyDomain");
            Debug.WriteLine("child domain: " + domain.FriendlyName);

            AppDomain.Unload(domain);
        }

        [TestMethod]
        public void Unload_Appdomain_twice()
        {
            AppDomain domain = AppDomain.CreateDomain("MyDomain");
            Debug.WriteLine("child domain: " + domain.FriendlyName);

            AppDomain.Unload(domain);

            //AppDomain.Unload(domain); => 
            //System.AppDomainUnloadedException: Attempted to access an unloaded appdomain. (Exception from HRESULT: 0x80131014)
        }

        [TestMethod]
        public void Get_current_domain_from_thread()
        {
            AppDomain domain = Thread.GetDomain();
            Debug.WriteLine("Thread executing in:");
            Debug.WriteLine(domain.FriendlyName);
        }

        [TestMethod]
        public void Instantiate_AppdomainSetup_print_properties()
        {
            AppDomainSetup ads = new AppDomainSetup();

            Debug.WriteLine($"{nameof(AppDomainSetup)} default properties: ");
            Debug.WriteLine($"{nameof(AppDomainSetup.ApplicationBase)} : {ads.ApplicationBase}"); // empty!!
            Debug.WriteLine($"{nameof(AppDomainSetup.ApplicationName)} : {ads.ApplicationName}"); // empty !!

            AppDomain ad2 = AppDomain.CreateDomain("ad2", null, ads);
            Debug.WriteLine(ad2.FriendlyName);
            Debug.WriteLine(ad2.BaseDirectory);
            Debug.WriteLine(ad2.SetupInformation.ApplicationBase); // Now its filled!!!

            AppDomain.Unload(ad2);
        }

        //[TestMethod]


        [TestMethod]
        public void Activate_object_in_other_Appdomain()
        {

            var baseDir = Thread.GetDomain().BaseDirectory;

            var ads = new AppDomainSetup
            {
                ApplicationBase = baseDir,
            };

            AppDomain ad2 = AppDomain.CreateDomain("ad2", null, ads);
            var t = typeof(MarshalByRefType);


            var proxy = ad2.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as MarshalByRefType;

            string callingDomainName = Thread.GetDomain().FriendlyName;

            proxy.SomeMethod(callingDomainName);
        }

        [TestMethod]
        public void Use_proxy_when_Appdomain_already_unload()
        {
            // create appdomain, create and use proxy
            var t = typeof(MarshalByRefType);

            var baseDir = Thread.GetDomain().BaseDirectory;
            var ads = new AppDomainSetup
            {
                ApplicationBase = baseDir,
            };

            AppDomain ad2 = AppDomain.CreateDomain("ad2", null, ads);
            var proxy = ad2.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as MarshalByRefType;
            string callingDomainName = Thread.GetDomain().FriendlyName;
            proxy.SomeMethod(callingDomainName);

            // unload
            AppDomain.Unload(ad2);

            //try use proxy
            //proxy.SomeMethod("Hello"); =>
            //throws System.AppDomainUnloadedException: Attempted to access an unloaded AppDomain.
        }

        [TestMethod] // WOORRRKSSSS!!!! 
        public void Appdomain_separates_static_state()
        {
            // Create separate Appdomains, create proxies
            var t = typeof(ClassWithStaticState);

            var baseDir = Thread.GetDomain().BaseDirectory;
            var ads = new AppDomainSetup
            {
                ApplicationBase = baseDir,
            };

            AppDomain ad1 = AppDomain.CreateDomain("ad1", null, ads);
            AppDomain ad2 = AppDomain.CreateDomain("ad2", null, ads);

            var proxy1 = ad1.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as ClassWithStaticState;
            var proxy2 = ad2.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as ClassWithStaticState;

            // Get state
            Debug.WriteLine($"State in ad1: {proxy1.GetState()}");
            Debug.WriteLine($"State in ad2: {proxy2.GetState()}");


            // Set different static states
            proxy1.Set("Hello");
            proxy2.Set("Bello");

            // Get state
            Debug.WriteLine($"State in ad1: {proxy1.GetState()}");
            Debug.WriteLine($"State in ad2: {proxy2.GetState()}");
        }

        [TestMethod]
        [Ignore]
        public void Verify_Appdomain_memory_footprint()
        {
            var t = typeof(Class1);
            var proxy_anchor = new Class1[100_000];
            var appdomain_anchor = new AppDomain[10_000];

            GC.Collect();
            long allocations_pre_start = GC.GetTotalMemory(true);
            Trace.WriteLine($"Current allocation (bytes): {allocations_pre_start}");

            // allocate inside current appdomain
            for (int i = 0; i < proxy_anchor.Length; i++)
                proxy_anchor[i] = new Class1();

            GC.Collect();
            long allocations_using_current_appdomain_1 = GC.GetTotalMemory(true);
            Trace.WriteLine($"Current allocation (bytes): {allocations_using_current_appdomain_1}");

            // allocate inside current appdomain
            for (int i = 0; i < proxy_anchor.Length; i++)
                proxy_anchor[i] = new Class1();

            GC.Collect();
            long allocations_using_current_appdomain_2 = GC.GetTotalMemory(true);
            Trace.WriteLine($"Current allocation (bytes): {allocations_using_current_appdomain_2}");

            var baseDir = Thread.GetDomain().BaseDirectory;
            var ads = new AppDomainSetup
            {
                ApplicationBase = baseDir,
            };

            // Create a lot of appdomains 
            for (int i = 0; i < 10_000; i++)
            {
                appdomain_anchor[i] = AppDomain.CreateDomain(string.Format("ad{0}", i), null, ads);
            }

            GC.Collect();
            long allocations_after_creating_appdomain = GC.GetTotalMemory(true);
            Trace.WriteLine($"Current allocation (bytes): {allocations_after_creating_appdomain}");
            // ~36KB per AppDomain


            // create proxies in appdomains            
            for (int i = 0; i < proxy_anchor.Length; i++)
            {
                int targetAD = i % 10_000;
                var proxy = appdomain_anchor[targetAD].CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as Class1;
                proxy_anchor[i] = proxy;
            }

            GC.Collect();
            long allocations_after_creating_proxies = GC.GetTotalMemory(true);
            Trace.WriteLine($"Current allocation (bytes): {allocations_after_creating_proxies}");
            // ~2,46KB per proxy


            // unload the appdomains
            foreach (var ad in appdomain_anchor)
                AppDomain.Unload(ad);

            appdomain_anchor = null;
            proxy_anchor = null;

            GC.Collect();
            long allocations_after_unloading_appdomains = GC.GetTotalMemory(true);
            Trace.WriteLine($"Current allocation (bytes): {allocations_after_unloading_appdomains}");

            // memory went down 616MB => 85MB
            // why not to baseline (~5,8MB) ?? 
        }
    }

    // Because this class is derived from MarshalByRefObject, a proxy
    // to a MarshalByRefType object can be returned across an AppDomain
    // boundary.
    public class MarshalByRefType : MarshalByRefObject
    {
        //  Call this method via a proxy.
        public void SomeMethod(string callingDomainName)
        {
            // Get this AppDomain's settings and display some of them.
            AppDomainSetup ads = AppDomain.CurrentDomain.SetupInformation;
            Debug.WriteLine("AppName={0}, AppBase={1}, ConfigFile={2}",
                ads.ApplicationName, ads.ApplicationBase, ads.ConfigurationFile);

            // Display the name of the calling AppDomain and the name of the second domain.
            // NOTE: The application's thread has transitioned between AppDomains.
            Debug.WriteLine("Calling from '{0}' to '{1}'.", callingDomainName, Thread.GetDomain().FriendlyName);
        }
    }

    public class ClassWithStaticState : MarshalByRefObject
    {
        protected static string _state = "unset";

        public void Set(string state) => _state = state;

        public string GetState() => _state;
    }

    public class Class1 : MarshalByRefObject
    {
        private string _state;

        public void Set(string state) => _state = state;

        public string GetState() => _state;
    }

    public class Class2 : MarshalByRefObject
    {
        public string Method1(string input)
            => (input ?? "").ToUpper().Replace(' ', '_');

        public Class3 Method2(string input)
            => new Class3(input);

        public Task Method3() => Task.CompletedTask;

        public ValueTask<int> Method4() => new ValueTask<int>(Task.FromResult(5));

        public PsResult Method5()
        => new PsResult(
            new PSObject[] { new PSObject(new Class3("alma")) },
            new object[] { 1, 2, 3 },
            new Exception("Hello from exception")
            );

    }

    public class Class3 : MarshalByRefObject
    {
        public Class3(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }


    public class Class4 : MarshalByRefObject
    {
        static Random _random = new Random();

        public Class4Result GetResult() => new Class4Result(SetupResult());

        Task<int> SetupResult()
        =>
            Task.Run(() =>
            {
                Thread.Sleep(5000);
                return Task.FromResult(_random.Next());
            });
    }

    public class Class4Result : MarshalByRefObject
    {

        public Task<int> Result { get; }

        public Class4Result(Task<int> result)
        {
            Result = result;
        }
    }


    [TestClass]
    public class AppdomainTests2
    {
        [TestMethod]
        public void Return_Poco_from_Appdomain()
        {
            // create appdomain, create and use proxy
            var t = typeof(Class2);


            var ads = new AppDomainSetup
            {
                ApplicationBase = Path.GetDirectoryName(t.Assembly.Location),
            };

            AppDomain ad1 = AppDomain.CreateDomain("ad1", null, ads);
            var proxy = ad1.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as Class2;

            var stringResult = proxy.Method1("Hello Appdomain world");
            Trace.WriteLine(stringResult);

            var objectResult = proxy.Method2("Hello Appdomain world");

            bool isOfClass3 = objectResult is Class3;
            var value = objectResult.Value;

            Trace.WriteLine(value);
        }

        [TestMethod]
        public void Return_Task_from_Appdomain_throws_not_serializable_ex()
        {
            // create appdomain, create and use proxy
            var t = typeof(Class2);
            var ads = new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(t.Assembly.Location), };
            AppDomain ad1 = AppDomain.CreateDomain("ad1", null, ads);
            var proxy = ad1.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as Class2;

            // var taskTresult = proxy.Method3();            
            // System.Runtime.Serialization.SerializationException: 'Type 'System.Threading.Tasks.Task' in
            // assembly 'mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
            // is not marked as serializable.'
        }

        [TestMethod]
        public void Return_ValueTask_from_Appdomain_throws_not_serializable_ex()
        {
            // create appdomain, create and use proxy
            var t = typeof(Class2);
            var ads = new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(t.Assembly.Location), };
            AppDomain ad1 = AppDomain.CreateDomain("ad1", null, ads);
            var proxy = ad1.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as Class2;

            //proxy.Method4(); same as above
        }

        [TestMethod]
        public void Return_PrResult_from_Appdomain_success()
        {
            // create appdomain, create and use proxy
            var t = typeof(Class2);
            var ads = new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(t.Assembly.Location), };
            AppDomain ad1 = AppDomain.CreateDomain("ad1", null, ads);
            var proxy = ad1.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as Class2;

            var psResult = proxy.Method5();
        }


        [TestMethod]
        public async Task Return_wrapped_hot_task_throws_not_serializable_exception()
        {
            // create appdomain, create and use proxy
            var t = typeof(Class4);
            var ads = new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(t.Assembly.Location), };
            AppDomain ad1 = AppDomain.CreateDomain("ad1", null, ads);
            var proxy = ad1.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as Class4;

            var resultProxy = proxy.GetResult();

            //var result = await resultProxy.Result; => Exception

            //Test method Tests.AppdomainTests2.Return_wrapped_hot_task threw exception: 
            //System.Runtime.Serialization.SerializationException:
            //Type 'System.Threading.Tasks.Task`1' in assembly 'mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
            //is not marked as serializable.
        }

        [TestMethod]
        public async Task Marshalling_LargeValueObj()
        {
            var t = typeof(TypeWithLargeValue);
            var ads = new AppDomainSetup { ApplicationBase = Path.GetDirectoryName(t.Assembly.Location), };
            AppDomain ad1 = AppDomain.CreateDomain("ad1", null, ads);
            var proxy = ad1.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName) as TypeWithLargeValue;

            var sw = Stopwatch.StartNew();
            var value = proxy.Value;
            Trace.WriteLine($"Ellapsed: {sw.ElapsedMilliseconds} ms"); // ~22sec
            sw.Stop();
        }


        public class TypeWithLargeValue : MarshalByRefObject
        {
            public Dictionary<string, string>[] Value { get; set; }

            public TypeWithLargeValue()
             => Value = Enumerable.Range(1, 50_000).Select(_ => GetDict()).ToArray();

            Dictionary<string, string> GetDict()
                => Enumerable
                    .Range(1, 100)
                    .ToDictionary(i => $"{i}_ajajjajajjaj338gbgggj", i => $"{Guid.NewGuid()}_{Guid.NewGuid()}");
        }

    }
}