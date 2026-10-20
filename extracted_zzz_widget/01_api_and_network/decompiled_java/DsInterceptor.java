package in;

import Cy.z0;
import T00.E;
import T00.F;
import T00.G;
import T00.w;
import Ym.C0867a;
import com.mihoyo.hyperion.net.aaaaa;
import com.mihoyo.hyperion.net.bbbbb;
import java.util.Set;
import kotlin.jvm.internal.K;

@x0.v(parameters = 0)
/* renamed from: in.m, reason: case insensitive filesystem */
/* loaded from: X:\AI project\解包\tools\classes2.dex */
public final class C1704m implements T00.w {
    public static final int c = 8;

    @G50.l
    public final int[] a;

    @G50.l
    public final Set<String> b;

    public C1704m(@G50.l int[] iArr) {
        K.p(iArr, "k2");
        this.a = iArr;
        this.b = z0.u(new String[]{"/apihub/app/api/signIn", "/game_record/app/genshin/aapi/widget/v2", "/game_record/app/hkrpg/aapi/widget"});
    }

    @G50.l
    public final int[] a() {
        return this.a;
    }

    public final boolean b(String str) {
        if (!yA.K.J2(str, "/", false, 2, (Object) null)) {
            str = "/" + str;
        }
        return this.b.contains(str);
    }

    @G50.l
    public G intercept(@G50.l w.a aVar) {
        boolean b;
        String a2222;
        K.p(aVar, "chain");
        E request = aVar.request();
        String i = request.i(C1705n.b);
        E.a n = request.n();
        String path = request.q().Z().getPath();
        if (i != null) {
            n.t(C1705n.b);
            b = K.g(i, C1705n.d);
        } else {
            K.m(path);
            b = b(path);
        }
        if (b) {
            n10.l lVar = new n10.l();
            F f = request.f();
            if (f != null) {
                f.writeTo(lVar);
            }
            String readString = lVar.readString(yA.g.b);
            String O = request.q().O();
            if (O == null) {
                O = "";
            }
            a2222 = aaaaa.a2222(readString, O);
        } else {
            a2222 = bbbbb.a2222(C0867a.a.a(this.a));
        }
        K.m(a2222);
        return aVar.proceed(n.a("DS", a2222).b());
    }
}
