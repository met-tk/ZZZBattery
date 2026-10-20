package in;

import Ce.l0;
import T00.E;
import T00.G;
import T00.w;
import Ym.d;
import android.net.Uri;
import android.text.TextUtils;
import hn.C1676n;
import java.io.IOException;
import java.util.List;
import java.util.Map;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import yA.P;

@x0.v(parameters = 1)
@r0({"SMAP\nLoginStatusInterceptor.kt\nKotlin\n*S Kotlin\n*F\n+ 1 LoginStatusInterceptor.kt\ncom/mihoyo/hyperion/net/interceptor/LoginStatusInterceptor\n+ 2 _Maps.kt\nkotlin/collections/MapsKt___MapsKt\n*L\n1#1,89:1\n216#2,2:90\n*S KotlinDebug\n*F\n+ 1 LoginStatusInterceptor.kt\ncom/mihoyo/hyperion/net/interceptor/LoginStatusInterceptor\n*L\n50#1:90,2\n*E\n"})
/* renamed from: in.u, reason: case insensitive filesystem */
/* loaded from: X:\AI project\解包\tools\classes2.dex */
public final class C1712u implements T00.w {
    public static final int a = 0;

    @G50.l
    public G intercept(@G50.l w.a aVar) throws IOException {
        String str;
        K.p(aVar, "chain");
        E request = aVar.request();
        E.a n = request.n();
        Eq.e eVar = Eq.e.a;
        String C = eVar.C();
        String w = eVar.w();
        String t = eVar.t();
        if (!K.g(C, "0") && !TextUtils.isEmpty(w)) {
            n.a("cookie", "stuid=" + C + ";stoken=" + w + ";mid=" + t + ";");
        }
        try {
            Uri parse = Uri.parse(Ym.e.a.b(d.a.API_BILIBILI));
            if (parse == null || (str = parse.getHost()) == null) {
                str = "";
            }
            String F = request.q().F();
            if (!P.O3(str) && !P.O3(F) && K.g(str, F)) {
                n.t("cookie");
            }
        } catch (Throwable unused) {
        }
        if (Cy.A.B8(Ym.e.a.d(), request.q().F())) {
            for (Map.Entry entry : l0.a.q().entrySet()) {
                n.a((String) entry.getKey(), (String) entry.getValue());
            }
            n.a(C1676n.b, Ym.e.a.e() ? "bjqfpuepfvuo" : "bll8iq97cem8");
        } else if (Cy.A.B8(new String[]{"img-static.mihoyo.com", "img-static.miyoushe.com"}, request.q().F())) {
            n.a("Referer", "https://app.mihoyo.com");
        }
        List k = request.k("otherUrl");
        if (!k.isEmpty()) {
            n.t("otherUrl");
            if (K.g((String) k.get(0), "true")) {
                n.t("Referer");
            }
        }
        return aVar.proceed(n.b());
    }
}
