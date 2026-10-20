package Ce;

import android.net.ConnectivityManager;
import android.net.LinkProperties;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.ArraySet;
import com.mihoyo.commlib.utils.LogUtils;
import com.mihoyo.hyperion.app.tasks.MihoyoAbTestInitTask;
import com.mihoyo.hyperion.bean.UpvoteType;
import java.lang.ref.WeakReference;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.function.BiConsumer;
import kotlin.jvm.functions.Function1;
import kotlin.jvm.functions.Function2;
import zy.e0;

@zy.l(message = "Use NetworkHelper instead")
@x0.v(parameters = 0)
@kotlin.jvm.internal.r0({"SMAP\nNetworkUtils.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NetworkUtils.kt\ncom/mihoyo/commlib/utils/NetworkUtils\n+ 2 _Collections.kt\nkotlin/collections/CollectionsKt___CollectionsKt\n*L\n1#1,328:1\n1869#2,2:329\n1869#2,2:331\n1869#2,2:333\n*S KotlinDebug\n*F\n+ 1 NetworkUtils.kt\ncom/mihoyo/commlib/utils/NetworkUtils\n*L\n124#1:329,2\n201#1:331,2\n213#1:333,2\n*E\n"})
/* renamed from: Ce.l0, reason: case insensitive filesystem */
/* loaded from: X:\AI project\解包\tools\classes11.dex */
public final class C0151l0 extends ConnectivityManager.NetworkCallback {
    public static final int b = 0;
    public static final int c = 1;
    public static final int d = 5;
    public static boolean e;
    public static boolean f;
    public static boolean g;
    public static boolean h;
    public static boolean i;

    @G50.l
    public static final C0151l0 a = new C0151l0();

    @G50.l
    public static final ArrayList<WeakReference<InterfaceC0143h0>> j = new ArrayList<>();

    @G50.l
    public static final Handler k = new Handler(Looper.getMainLooper());
    public static final int l = 8;

    public static final void j(WeakReference weakReference) {
        InterfaceC0143h0 interfaceC0143h0 = (InterfaceC0143h0) weakReference.get();
        if (interfaceC0143h0 != null) {
            interfaceC0143h0.c(e, f, g);
        }
    }

    public static final void u(Function2 function2, Object obj, Object obj2) {
        function2.invoke(obj, obj2);
    }

    public static final zy.N0 v(Map map, String str, String str2) {
        kotlin.jvm.internal.K.p(str, "key");
        kotlin.jvm.internal.K.p(str2, "value");
        if (!kotlin.jvm.internal.K.g(str, "x-rpc-client_type") && !kotlin.jvm.internal.K.g(str, "Referer")) {
            map.put(str, str2);
        }
        return zy.N0.a;
    }

    public final int d() {
        int e2 = e();
        if (f != h || g != i) {
            i();
        }
        return e2;
    }

    public final int e() {
        int f2 = f();
        LogUtils.INSTANCE.d("NetworkState: " + f2);
        boolean z = f2 == 1;
        boolean z2 = f2 == 5;
        e = f2 != 0;
        h = f;
        i = g;
        f = z;
        g = z2;
        return f2;
    }

    public final int f() {
        ConnectivityManager connectivityManager;
        Network activeNetwork;
        NetworkCapabilities networkCapabilities;
        Object systemService = C0158p.b().getSystemService("connectivity");
        if (!(systemService instanceof ConnectivityManager) || (activeNetwork = (connectivityManager = (ConnectivityManager) systemService).getActiveNetwork()) == null || (networkCapabilities = connectivityManager.getNetworkCapabilities(activeNetwork)) == null) {
            return 0;
        }
        if ((networkCapabilities.hasTransport(1) || networkCapabilities.hasTransport(5) || networkCapabilities.hasTransport(8) || networkCapabilities.hasTransport(3) || networkCapabilities.hasTransport(2)) && networkCapabilities.hasCapability(12)) {
            return 1;
        }
        return networkCapabilities.hasCapability(12) ? 5 : 0;
    }

    @G50.l
    public final String g() {
        return "Android/" + Build.VERSION.RELEASE + " miyoushe/" + C0167u.a.h(C0158p.b());
    }

    public final void h() {
        Object systemService = C0158p.b().getSystemService("connectivity");
        if (systemService instanceof ConnectivityManager) {
            try {
                e0.a aVar = zy.e0.b;
                ((ConnectivityManager) systemService).registerDefaultNetworkCallback(a);
                zy.e0.b(zy.N0.a);
            } catch (Throwable th) {
                e0.a aVar2 = zy.e0.b;
                zy.e0.b(zy.f0.a(th));
            }
        }
        x();
    }

    public final void i() {
        ArrayList<WeakReference<InterfaceC0143h0>> arrayList = j;
        synchronized (arrayList) {
            try {
                Iterator<T> it = arrayList.iterator();
                while (it.hasNext()) {
                    final WeakReference weakReference = (WeakReference) it.next();
                    k.post(new Runnable() { // from class: Ce.k0
                        @Override // java.lang.Runnable
                        public final void run() {
                            C0151l0.j(weakReference);
                        }
                    });
                }
                zy.N0 n0 = zy.N0.a;
            } catch (Throwable th) {
                throw th;
            }
        }
    }

    public final boolean k() {
        return e;
    }

    public final boolean l() {
        return g;
    }

    public final boolean m() {
        return i;
    }

    public final boolean n() {
        return h;
    }

    public final boolean o() {
        return f;
    }

    @Override // android.net.ConnectivityManager.NetworkCallback
    public void onCapabilitiesChanged(@G50.l Network network, @G50.l NetworkCapabilities networkCapabilities) {
        kotlin.jvm.internal.K.p(network, "network");
        kotlin.jvm.internal.K.p(networkCapabilities, "networkCapabilities");
        super.onCapabilitiesChanged(network, networkCapabilities);
        x();
    }

    @Override // android.net.ConnectivityManager.NetworkCallback
    public void onLinkPropertiesChanged(@G50.l Network network, @G50.l LinkProperties linkProperties) {
        kotlin.jvm.internal.K.p(network, "network");
        kotlin.jvm.internal.K.p(linkProperties, "linkProperties");
        super.onLinkPropertiesChanged(network, linkProperties);
        x();
    }

    public final void p(@G50.l InterfaceC0143h0 interfaceC0143h0) {
        kotlin.jvm.internal.K.p(interfaceC0143h0, "listener");
        ArrayList<WeakReference<InterfaceC0143h0>> arrayList = j;
        synchronized (arrayList) {
            Iterator<T> it = arrayList.iterator();
            while (it.hasNext()) {
                if (((WeakReference) it.next()).get() == interfaceC0143h0) {
                    return;
                }
            }
            j.add(new WeakReference<>(interfaceC0143h0));
        }
    }

    @G50.l
    public final Map<String, String> q() {
        zy.V v = new zy.V("x-rpc-client_type", MihoyoAbTestInitTask.PLATFORM_VALUE);
        C0167u c0167u = C0167u.a;
        return Cy.o0.W(new zy.V[]{v, new zy.V("x-rpc-app_version", c0167u.h(C0158p.b())), new zy.V("x-rpc-sys_version", Build.VERSION.RELEASE), new zy.V("x-rpc-channel", com.mihoyo.commlib.utils.b.a.e()), new zy.V("x-rpc-device_id", c0167u.m()), new zy.V("x-rpc-device_fp", c0167u.l()), new zy.V("x-rpc-device_name", c0167u.q()), new zy.V("x-rpc-device_model", Build.MODEL), new zy.V("x-rpc-h265_supported", c0167u.B() ? UpvoteType.DEFAULT_LIKE : UpvoteType.NONE), new zy.V("Referer", "https://app.mihoyo.com")});
    }

    @G50.l
    public final Map<String, String> r() {
        zy.V v = new zy.V("x-rpc-client_type", MihoyoAbTestInitTask.PLATFORM_VALUE);
        C0167u c0167u = C0167u.a;
        Map<String, String> j0 = Cy.o0.j0(new zy.V[]{v, new zy.V("x-rpc-app_version", c0167u.h(C0158p.b())), new zy.V("x-rpc-sys_version", Build.VERSION.RELEASE), new zy.V("x-rpc-channel", com.mihoyo.commlib.utils.b.a.e()), new zy.V("x-rpc-device_id", c0167u.m()), new zy.V("x-rpc-device_fp", c0167u.l()), new zy.V("x-rpc-device_name", c0167u.q()), new zy.V("x-rpc-device_model", Build.MODEL), new zy.V("Referer", "https://app.mihoyo.com")});
        Map<String, String> i2 = Xe.t.a.i();
        if (i2 == null) {
            i2 = Cy.o0.z();
        }
        j0.putAll(i2);
        return j0;
    }

    public final void s(@G50.l Function1<? super Boolean, zy.N0> function1) {
        kotlin.jvm.internal.K.p(function1, "callback");
        x();
        function1.invoke(Boolean.valueOf(e));
    }

    @G50.l
    public final Map<String, String> t() {
        Map<String, String> r = r();
        final LinkedHashMap linkedHashMap = new LinkedHashMap();
        final Function2 function2 = new Function2() { // from class: Ce.i0
            public final Object invoke(Object obj, Object obj2) {
                zy.N0 v;
                v = C0151l0.v(linkedHashMap, (String) obj, (String) obj2);
                return v;
            }
        };
        r.forEach(new BiConsumer() { // from class: Ce.j0
            @Override // java.util.function.BiConsumer
            public final void accept(Object obj, Object obj2) {
                C0151l0.u(function2, obj, obj2);
            }
        });
        return linkedHashMap;
    }

    public final void w(@G50.l InterfaceC0143h0 interfaceC0143h0) {
        kotlin.jvm.internal.K.p(interfaceC0143h0, "listener");
        ArrayList<WeakReference<InterfaceC0143h0>> arrayList = j;
        synchronized (arrayList) {
            try {
                ArraySet arraySet = new ArraySet();
                Iterator<T> it = arrayList.iterator();
                while (it.hasNext()) {
                    WeakReference weakReference = (WeakReference) it.next();
                    InterfaceC0143h0 interfaceC0143h02 = (InterfaceC0143h0) weakReference.get();
                    if (interfaceC0143h02 == null || interfaceC0143h02 == interfaceC0143h0) {
                        arraySet.add(weakReference);
                    }
                }
                j.removeAll(arraySet);
            } catch (Throwable th) {
                throw th;
            }
        }
    }

    public final int x() {
        return d();
    }
}
