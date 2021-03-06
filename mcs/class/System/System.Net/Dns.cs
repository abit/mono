// System.Net.Dns.cs
//
// Authors:
//	Mads Pultz (mpultz@diku.dk)
//	Lawrence Pit (loz@cable.a2000.nl)

// Author: Mads Pultz (mpultz@diku.dk)
// 	   Lawrence Pit (loz@cable.a2000.nl)
//	   Marek Safar (marek.safar@gmail.com)
// 	   Gonzalo Paniagua Javier (gonzalo.mono@gmail.com)
//
// (C) Mads Pultz, 2001
// Copyright (c) 2011 Novell, Inc.
// Copyright (c) 2011 Xamarin, Inc.

//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
#if NET_4_5
using System.Threading.Tasks;
#endif

#if !MOBILE
using Mono.Net.Dns;
#endif

namespace System.Net {
	public static class Dns {
#if !MOBILE
		static bool use_mono_dns;
		static SimpleResolver resolver;
#endif

		static Dns ()
		{
			System.Net.Sockets.Socket.CheckProtocolSupport();

#if !MOBILE
			if (Environment.GetEnvironmentVariable ("MONO_DNS") != null) {
				resolver = new SimpleResolver ();
				use_mono_dns = true;
			}
#endif
		}

#if !MOBILE
		internal static bool UseMonoDns {
			get { return use_mono_dns; }
		}
#endif

#if !MOONLIGHT // global remove of async methods


		private delegate IPHostEntry GetHostByNameCallback (string hostName);
		private delegate IPHostEntry ResolveCallback (string hostName);
		private delegate IPHostEntry GetHostEntryNameCallback (string hostName);
		private delegate IPHostEntry GetHostEntryIPCallback (IPAddress hostAddress);
		private delegate IPAddress [] GetHostAddressesCallback (string hostName);

#if !MOBILE
		static void OnCompleted (object sender, SimpleResolverEventArgs e)
		{
			DnsAsyncResult ares = (DnsAsyncResult) e.UserToken;
			IPHostEntry entry = e.HostEntry;
			if (entry == null || e.ResolverError != 0) {
				ares.SetCompleted (false, new Exception ("Error: " + e.ResolverError));
				return;
			}
			ares.SetCompleted (false, entry);
		}

		static IAsyncResult BeginAsyncCallAddresses (string host, AsyncCallback callback, object state)
		{
			SimpleResolverEventArgs e = new SimpleResolverEventArgs ();
			e.Completed += OnCompleted;
			e.HostName = host;
			DnsAsyncResult ares = new DnsAsyncResult (callback, state);
			e.UserToken = ares;
			if (resolver.GetHostAddressesAsync (e) == false)
				ares.SetCompleted (true, e.HostEntry); // Completed synchronously
			return ares;
		}

		static IAsyncResult BeginAsyncCall (string host, AsyncCallback callback, object state)
		{
			SimpleResolverEventArgs e = new SimpleResolverEventArgs ();
			e.Completed += OnCompleted;
			e.HostName = host;
			DnsAsyncResult ares = new DnsAsyncResult (callback, state);
			e.UserToken = ares;
			if (resolver.GetHostEntryAsync (e) == false)
				ares.SetCompleted (true, e.HostEntry); // Completed synchronously
			return ares;
		}

		static IPHostEntry EndAsyncCall (DnsAsyncResult ares)
		{
			if (ares == null)
				throw new ArgumentException ("Invalid asyncResult");
			if (!ares.IsCompleted)
				ares.AsyncWaitHandle.WaitOne ();
			if (ares.Exception != null)
				throw ares.Exception;
			IPHostEntry entry = ares.HostEntry;
			if (entry == null || entry.AddressList == null || entry.AddressList.Length == 0)
				throw new SocketException(11001);
			return entry;
		}
#endif

		[Obsolete ("Use BeginGetHostEntry instead")]
		public static IAsyncResult BeginGetHostByName (string hostName, AsyncCallback requestCallback, object stateObject)
		{
			if (hostName == null)
				throw new ArgumentNullException ("hostName");

#if !MOBILE
			if (use_mono_dns)
				return BeginAsyncCall (hostName, requestCallback, stateObject);
#endif

			GetHostByNameCallback c = new GetHostByNameCallback (GetHostByName);
			return c.BeginInvoke (hostName, requestCallback, stateObject);
		}

		[Obsolete ("Use BeginGetHostEntry instead")]
		public static IAsyncResult BeginResolve (string hostName, AsyncCallback requestCallback, object stateObject)
		{
			if (hostName == null)
				throw new ArgumentNullException ("hostName");

#if !MOBILE
			if (use_mono_dns)
				return BeginAsyncCall (hostName, requestCallback, stateObject);
#endif

			ResolveCallback c = new ResolveCallback (Resolve);
			return c.BeginInvoke (hostName, requestCallback, stateObject);
		}

		public static IAsyncResult BeginGetHostAddresses (string hostNameOrAddress, AsyncCallback requestCallback, object stateObject)
		{
			if (hostNameOrAddress == null)
				throw new ArgumentNullException ("hostName");
			if (hostNameOrAddress == "0.0.0.0" || hostNameOrAddress == "::0")
				throw new ArgumentException ("Addresses 0.0.0.0 (IPv4) " +
					"and ::0 (IPv6) are unspecified addresses. You " +
					"cannot use them as target address.",
					"hostNameOrAddress");

#if !MOBILE
			if (use_mono_dns)
				return BeginAsyncCallAddresses (hostNameOrAddress, requestCallback, stateObject);
#endif

			GetHostAddressesCallback c = new GetHostAddressesCallback (GetHostAddresses);
			return c.BeginInvoke (hostNameOrAddress, requestCallback, stateObject);
		}

		public static IAsyncResult BeginGetHostEntry (string hostNameOrAddress, AsyncCallback requestCallback, object stateObject)
		{
			if (hostNameOrAddress == null)
				throw new ArgumentNullException ("hostName");
			if (hostNameOrAddress == "0.0.0.0" || hostNameOrAddress == "::0")
				throw new ArgumentException ("Addresses 0.0.0.0 (IPv4) " +
					"and ::0 (IPv6) are unspecified addresses. You " +
					"cannot use them as target address.",
					"hostNameOrAddress");

#if !MOBILE
			if (use_mono_dns)
				return BeginAsyncCall (hostNameOrAddress, requestCallback, stateObject);
#endif

			GetHostEntryNameCallback c = new GetHostEntryNameCallback (GetHostEntry);
			return c.BeginInvoke (hostNameOrAddress, requestCallback, stateObject);
		}

		public static IAsyncResult BeginGetHostEntry (IPAddress address, AsyncCallback requestCallback, object stateObject)
		{
			if (address == null)
				throw new ArgumentNullException ("address");

#if !MOBILE
			if (use_mono_dns)
				return BeginAsyncCall (address.ToString (), requestCallback, stateObject);
#endif

			GetHostEntryIPCallback c = new GetHostEntryIPCallback (GetHostEntry);
			return c.BeginInvoke (address, requestCallback, stateObject);
		}

		[Obsolete ("Use EndGetHostEntry instead")]
		public static IPHostEntry EndGetHostByName (IAsyncResult asyncResult) 
		{
			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

#if !MOBILE
			if (use_mono_dns)
				return EndAsyncCall (asyncResult as DnsAsyncResult);
#endif

			AsyncResult async = (AsyncResult) asyncResult;
			GetHostByNameCallback cb = (GetHostByNameCallback) async.AsyncDelegate;
			return cb.EndInvoke(asyncResult);
		}

		[Obsolete ("Use EndGetHostEntry instead")]
		public static IPHostEntry EndResolve (IAsyncResult asyncResult) 
		{
			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

#if !MOBILE
			if (use_mono_dns)
				return EndAsyncCall (asyncResult as DnsAsyncResult);
#endif

			AsyncResult async = (AsyncResult) asyncResult;
			ResolveCallback cb = (ResolveCallback) async.AsyncDelegate;
			return cb.EndInvoke(asyncResult);
		}

		public static IPAddress [] EndGetHostAddresses (IAsyncResult asyncResult) 
		{
			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

#if !MOBILE
			if (use_mono_dns) {
				IPHostEntry entry = EndAsyncCall (asyncResult as DnsAsyncResult);
				if (entry == null)
					return null;
				return entry.AddressList;
			}
#endif

			AsyncResult async = (AsyncResult) asyncResult;
			GetHostAddressesCallback cb = (GetHostAddressesCallback) async.AsyncDelegate;
			return cb.EndInvoke(asyncResult);
		}

		public static IPHostEntry EndGetHostEntry (IAsyncResult asyncResult) 
		{
			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

#if !MOBILE
			if (use_mono_dns)
				return EndAsyncCall (asyncResult as DnsAsyncResult);
#endif

			AsyncResult async = (AsyncResult) asyncResult;
			if (async.AsyncDelegate is GetHostEntryIPCallback)
				return ((GetHostEntryIPCallback) async.AsyncDelegate).EndInvoke (asyncResult);
			GetHostEntryNameCallback cb = (GetHostEntryNameCallback) async.AsyncDelegate;
			return cb.EndInvoke(asyncResult);
		}
		
#endif // !MOONLIGHT: global remove of async methods

#if !TARGET_JVM
		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		private extern static bool GetHostByName_internal(string host, out string h_name, out string[] h_aliases, out string[] h_addr_list);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		private extern static bool GetHostByAddr_internal(string addr, out string h_name, out string[] h_aliases, out string[] h_addr_list);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		private extern static bool GetHostName_internal(out string h_name);
#endif	

		private static IPHostEntry hostent_to_IPHostEntry(string h_name, string[] h_aliases, string[] h_addrlist) 
		{
			IPHostEntry he = new IPHostEntry();
			ArrayList addrlist = new ArrayList();

			he.HostName = h_name;
			he.Aliases = h_aliases;
			for(int i=0; i<h_addrlist.Length; i++) {
				try {
					IPAddress newAddress = IPAddress.Parse(h_addrlist[i]);

					if( (Socket.SupportsIPv6 && newAddress.AddressFamily == AddressFamily.InterNetworkV6) ||
					    (Socket.SupportsIPv4 && newAddress.AddressFamily == AddressFamily.InterNetwork) )
						addrlist.Add(newAddress);
				} catch (ArgumentNullException) {
					/* Ignore this, as the
					 * internal call might have
					 * left some blank entries at
					 * the end of the array
					 */
				}
			}

			if(addrlist.Count == 0)
				throw new SocketException(11001);

			he.AddressList = addrlist.ToArray(typeof(IPAddress)) as IPAddress[];
			return he;
		}

		[Obsolete ("Use GetHostEntry instead")]
		public static IPHostEntry GetHostByAddress(IPAddress address)
		{
			if (address == null)
				throw new ArgumentNullException ("address");

			return GetHostByAddressFromString (address.ToString (), false);
		}

		[Obsolete ("Use GetHostEntry instead")]
		public static IPHostEntry GetHostByAddress(string address)
		{
			if (address == null)
				throw new ArgumentNullException ("address");

			return GetHostByAddressFromString (address, true);
		}

		static IPHostEntry GetHostByAddressFromString (string address, bool parse)
		{
			// Undocumented MS behavior: when called with IF_ANY,
			// this should return the local host
			if (address.Equals ("0.0.0.0")) {
				address = "127.0.0.1";
				parse = false;
			}

			// Must check the IP format, might send an exception if invalid string.
			if (parse)
				IPAddress.Parse (address);

			string h_name;
			string[] h_aliases, h_addrlist;
#if TARGET_JVM
			h_name = null;
			h_aliases = null;
			h_addrlist = null;
            		try {
                		java.net.InetAddress[] iaArr = 
					java.net.InetAddress.getAllByName(address);
		                if (iaArr != null && iaArr.Length > 0)
                		    h_name = iaArr[0].getHostName();
		                if (iaArr != null && iaArr.Length > 0)
                		{
		                    h_addrlist = new String[iaArr.Length];
                		    for (int i = 0; i < h_addrlist.Length; i++)
		                        h_addrlist[i] = iaArr[i].getHostAddress();
                		}
            		} catch (java.net.UnknownHostException jUHE) {
		                throw new SocketException((int)SocketError.HostNotFound, jUHE.Message);
            		}
#else
			bool ret = GetHostByAddr_internal(address, out h_name, out h_aliases, out h_addrlist);
			if (!ret)
				throw new SocketException(11001);
#endif
			return (hostent_to_IPHostEntry (h_name, h_aliases, h_addrlist));
			
		}

		public static IPHostEntry GetHostEntry (string hostNameOrAddress)
		{
			if (hostNameOrAddress == null)
				throw new ArgumentNullException ("hostNameOrAddress");
			if (hostNameOrAddress == "0.0.0.0" || hostNameOrAddress == "::0")
				throw new ArgumentException ("Addresses 0.0.0.0 (IPv4) " +
					"and ::0 (IPv6) are unspecified addresses. You " +
					"cannot use them as target address.",
					"hostNameOrAddress");

			IPAddress addr;
			if (hostNameOrAddress.Length > 0 && IPAddress.TryParse (hostNameOrAddress, out addr))
				return GetHostEntry (addr);

			return GetHostByName (hostNameOrAddress);
		}

		public static IPHostEntry GetHostEntry (IPAddress address)
		{
			if (address == null)
				throw new ArgumentNullException ("address");

			return GetHostByAddressFromString (address.ToString (), false);
		}

		public static IPAddress [] GetHostAddresses (string hostNameOrAddress)
		{
			if (hostNameOrAddress == null)
				throw new ArgumentNullException ("hostNameOrAddress");

			if (hostNameOrAddress == "0.0.0.0" || hostNameOrAddress == "::0")
				throw new ArgumentException ("Addresses 0.0.0.0 (IPv4) " +
					"and ::0 (IPv6) are unspecified addresses. You " +
					"cannot use them as target address.",
					"hostNameOrAddress");

			IPAddress addr;
			if (hostNameOrAddress.Length > 0 && IPAddress.TryParse (hostNameOrAddress, out addr))
				return new IPAddress[1] { addr };

			return GetHostEntry (hostNameOrAddress).AddressList;
		}

		[Obsolete ("Use GetHostEntry instead")]
		public static IPHostEntry GetHostByName (string hostName)
		{
			if (hostName == null)
				throw new ArgumentNullException ("hostName");
#if TARGET_JVM
			if (hostName.Length == 0)
				hostName = "localhost";
		        try {
				java.net.InetAddress[] iaArr = java.net.InetAddress.getAllByName(hostName);
				IPHostEntry host = new IPHostEntry();
				if (iaArr != null && iaArr.Length > 0)
                		{
					host.HostName = iaArr[0].getHostName();
                		    	IPAddress[] ipArr = new IPAddress[iaArr.Length];
		                    	for (int i = 0; i < iaArr.Length; i++)
                		        	ipArr[i] = IPAddress.Parse(iaArr[i].getHostAddress());

					host.AddressList = ipArr;
                		}
		                return host;
			} catch (java.net.UnknownHostException jUHE) {
				throw new SocketException((int)SocketError.HostNotFound, jUHE.Message);
			}
#else
			string h_name;
			string[] h_aliases, h_addrlist;

			bool ret = GetHostByName_internal(hostName, out h_name, out h_aliases, out h_addrlist);
			if (ret == false)
				throw new SocketException(11001);

			return(hostent_to_IPHostEntry(h_name, h_aliases, h_addrlist));
#endif
		}

		public static string GetHostName ()
		{
#if TARGET_JVM
			return java.net.InetAddress.getLocalHost ().getHostName ();
#else
			string hostName;

			bool ret = GetHostName_internal(out hostName);

			if (ret == false)
				throw new SocketException(11001);

			return hostName;
#endif
		}

		[Obsolete ("Use GetHostEntry instead")]
		public static IPHostEntry Resolve(string hostName) 
		{
			if (hostName == null)
				throw new ArgumentNullException ("hostName");

			IPHostEntry ret = null;

			try {
				ret =  GetHostByAddress(hostName);
			}
			catch{}

			if(ret == null)
				ret =  GetHostByName(hostName);

			return ret;
		}

#if NET_4_5
		public static Task<IPAddress[]> GetHostAddressesAsync (string hostNameOrAddress)
		{
			return Task<IPAddress[]>.Factory.FromAsync (BeginGetHostAddresses, EndGetHostAddresses, hostNameOrAddress, null);
		}

		public static Task<IPHostEntry> GetHostEntryAsync (IPAddress address)
		{
			return Task<IPHostEntry>.Factory.FromAsync (BeginGetHostEntry, EndGetHostEntry, address, null);
		}

		public static Task<IPHostEntry> GetHostEntryAsync (string hostNameOrAddress)
		{
			return Task<IPHostEntry>.Factory.FromAsync (BeginGetHostEntry, EndGetHostEntry, hostNameOrAddress, null);
		}
#endif
	}
}

