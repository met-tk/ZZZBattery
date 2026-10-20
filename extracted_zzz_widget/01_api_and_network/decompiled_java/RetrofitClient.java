package Ym;

import Cy.H;
import Cy.S;
import T00.e;
import T00.w;
import a60.u;
import an.C1018b;
import an.C1023g;
import com.mihoyo.commlib.image.MiHoYoGlideModule;
import com.mihoyo.commlib.utils.LogUtils;
import com.mihoyo.hyperion.manager.AppConfig;
import com.mihoyo.hyperion.manager.AppConfigInfo;
import com.mihoyo.hyperion.post.comment.CommentDetailActivity;
import in.C1694c;
import in.C1713v;
import java.io.File;
import java.util.HashMap;
import java.util.List;
import java.util.concurrent.TimeUnit;
import jn.C1748a;
import jn.C1749b;
import jn.C1752e;
import kn.C1765e;
import kotlin.Lazy;
import kotlin.jvm.functions.Function0;
import kotlin.jvm.functions.Function1;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import okhttp3.OkHttpClient;
import zy.G;
import zy.N0;

@x0.v(parameters = 0)
@r0({"SMAP\nRetrofitClient.kt\nKotlin\n*S Kotlin\n*F\n+ 1 RetrofitClient.kt\ncom/mihoyo/hyperion/net/RetrofitClient\n+ 2 fake.kt\nkotlin/jvm/internal/FakeKt\n*L\n1#1,204:1\n1#2:205\n*E\n"})
/* loaded from: X:\AI project\解包\tools\classes2.dex */
public final class A {
    public static OkHttpClient b = null;
    public static a60.u c = null;
    public static final long f = 15;

    @G50.l
    public static final String g = "https://bbs-api.miyoushe.com";
    public static boolean i;

    @G50.l
    public static final A a = new A();

    @G50.l
    public static final Lazy d = G.b(new Function0() { // from class: Ym.x
        public final Object invoke() {
            OkHttpClient s;
            s = A.s();
            return s;
        }
    });

    @G50.l
    public static final Lazy e = G.b(new Function0() { // from class: Ym.y
        public final Object invoke() {
            a60.u e2;
            e2 = A.e();
            return e2;
        }
    });

    @G50.l
    public static final HashMap<Integer, Object> h = new HashMap<>();
    public static final int j = 8;

    /* loaded from: X:\AI project\解包\tools\classes2.dex */
    public static final class a implements C1713v.d {
        @Override // in.C1713v.d
        public void log(String str) {
            K.p(str, CommentDetailActivity.o);
            try {
                LogUtils.INSTANCE.i("NetworkLog", str);
            } catch (Exception e) {
                e.printStackTrace();
            }
        }
    }

    public static final a60.u e() {
        OkHttpClient okHttpClient = b;
        OkHttpClient okHttpClient2 = null;
        if (okHttpClient == null) {
            K.S("okHttpClient");
            okHttpClient = null;
        }
        List<? extends T00.w> b6 = S.b6(okHttpClient.interceptors());
        OkHttpClient okHttpClient3 = b;
        if (okHttpClient3 == null) {
            K.S("okHttpClient");
        } else {
            okHttpClient2 = okHttpClient3;
        }
        b6.addAll(okHttpClient2.networkInterceptors());
        return new u.b().h(new C1694c(bn.l.a.n(b6))).b(new ye.a()).a(new C1765e(C1749b.n)).a(b60.h.d()).d(g).f();
    }

    public static /* synthetic */ void k() {
    }

    public static /* synthetic */ void o() {
    }

    public static final T00.G q(w.a aVar) {
        K.p(aVar, "chain");
        return aVar.proceed(aVar.request());
    }

    public static final OkHttpClient s() {
        if (b == null) {
            throw new IllegalStateException("RetrofitClient is not initialized yet.");
        }
        List S = H.S(new T00.r[]{new C1752e(false, new Function0() { // from class: Ym.z
            public final Object invoke() {
                OkHttpClient t;
                t = A.t();
                return t;
            }
        })});
        if (C1023g.a.j()) {
            S.add(new C1018b());
        }
        OkHttpClient okHttpClient = b;
        if (okHttpClient == null) {
            K.S("okHttpClient");
            okHttpClient = null;
        }
        return f.a(okHttpClient.newBuilder().r(new C1748a(S)));
    }

    public static final OkHttpClient t() {
        OkHttpClient okHttpClient = b;
        if (okHttpClient != null) {
            return okHttpClient;
        }
        K.S("okHttpClient");
        return null;
    }

    public final T00.c f() {
        File file = new File(Ce.p.b().getCacheDir(), "app_cache");
        if (file.exists()) {
            return new T00.c(file, 10485760L);
        }
        return null;
    }

    public final a60.u g() {
        Object value = e.getValue();
        K.o(value, "getValue(...)");
        return (a60.u) value;
    }

    @G50.l
    public final A h() {
        return a;
    }

    public final OkHttpClient i() {
        return (OkHttpClient) d.getValue();
    }

    @G50.l
    public final OkHttpClient j() {
        return i();
    }

    @zy.l(message = "废弃，不要再使用，为单一特殊场景增加的")
    public final <T> T l(@G50.l Class<T> cls) {
        K.p(cls, "clazz");
        a60.u uVar = c;
        if (uVar == null) {
            K.S("retrofit");
            uVar = null;
        }
        return (T) uVar.g(cls);
    }

    public final /* synthetic */ <T> T m() {
        K.y(4, "T");
        return (T) n(Object.class);
    }

    @zy.l(message = "废弃，不要再使用，请使用上面inline getOrCreateService()方法 ")
    public final <T> T n(@G50.l Class<T> cls) {
        K.p(cls, "clazz");
        HashMap<Integer, Object> hashMap = h;
        Object obj = (T) hashMap.get(Integer.valueOf(cls.getName().hashCode()));
        if (obj == null) {
            if (bn.l.a.r()) {
                obj = (T) g().g(cls);
            } else {
                a60.u uVar = c;
                if (uVar == null) {
                    K.S("retrofit");
                    uVar = null;
                }
                obj = uVar.g(cls);
            }
            Integer valueOf = Integer.valueOf(cls.getName().hashCode());
            K.m(obj);
            hashMap.put(valueOf, obj);
        }
        return (T) obj;
    }

    public final void p(@G50.l Function1<? super OkHttpClient.a, N0> function1) {
        K.p(function1, "config");
        T00.c f2 = f();
        T00.w wVar = new T00.w() { // from class: Ym.w
            public final T00.G intercept(w.a aVar) {
                T00.G q;
                q = A.q(aVar);
                return q;
            }
        };
        C1713v c1713v = new C1713v(new a());
        c1713v.f(C1713v.b.BASIC);
        OkHttpClient.a g2 = ye.d.a(new OkHttpClient.a()).d(c1713v).c(wVar).g(f2);
        TimeUnit timeUnit = TimeUnit.SECONDS;
        OkHttpClient.a R0 = g2.k(15L, timeUnit).j0(15L, timeUnit).R0(15L, timeUnit);
        AppConfigInfo appConfigInfo = AppConfig.get();
        if (appConfigInfo.getEnableCallTimeout()) {
            R0.h(appConfigInfo.getCallTimeoutSeconds(), timeUnit);
        }
        function1.invoke(R0);
        b = f.a(R0);
        bn.l lVar = bn.l.a;
        if (lVar.r()) {
            lVar.v();
        }
        MiHoYoGlideModule.Companion.c(j());
        u.b bVar = new u.b();
        OkHttpClient okHttpClient = b;
        e.a aVar = null;
        if (okHttpClient == null) {
            K.S("okHttpClient");
            okHttpClient = null;
        }
        u.b j2 = bVar.j(okHttpClient);
        e.a aVar2 = b;
        if (aVar2 == null) {
            K.S("okHttpClient");
        } else {
            aVar = aVar2;
        }
        c = j2.h(new C1694c(aVar)).b(new ye.a()).a(new C1765e("okhttp")).a(b60.h.d()).d(g).f();
    }

    public final boolean r() {
        return i;
    }

    public final void u(boolean z) {
        i = z;
    }
}
