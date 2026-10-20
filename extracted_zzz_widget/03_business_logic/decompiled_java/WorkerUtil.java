package hg;

import android.content.Context;
import androidx.work.ListenableWorker;
import com.mihoyo.commlib.utils.LogUtils;
import com.mihoyo.hyperion.manager.AppConfigKt;
import java.util.concurrent.TimeUnit;
import kotlin.jvm.internal.K;
import w5.D;
import w5.r;
import w5.w;
import x0.v;

@v(parameters = 1)
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class p {

    @G50.l
    public static final p a = new p();
    public static final int b = 0;

    public final /* synthetic */ <T extends ListenableWorker> void a(Context context) {
        K.p(context, "context");
        K.y(4, "T");
        String simpleName = ListenableWorker.class.getSimpleName();
        LogUtils.INSTANCE.i(l.b, "cancel: " + simpleName);
        D.p(context).g(simpleName);
    }

    public final /* synthetic */ <T extends ListenableWorker> void b(Context context) {
        K.p(context, "context");
        K.y(4, "T");
        c(context, ListenableWorker.class);
    }

    public final <T extends ListenableWorker> void c(@G50.l Context context, @G50.l Class<T> cls) {
        K.p(context, "context");
        K.p(cls, "clazz");
        r f = r.f(cls);
        K.o(f, "from(...)");
        D.p(context).k(f);
    }

    public final /* synthetic */ <T extends ListenableWorker> void d(Context context) {
        K.p(context, "context");
        K.y(4, "T");
        e(context, ListenableWorker.class);
    }

    public final <T extends ListenableWorker> void e(@G50.l Context context, @G50.l Class<T> cls) {
        K.p(context, "context");
        K.p(cls, "clazz");
        String simpleName = cls.getSimpleName();
        LogUtils.INSTANCE.i(l.b, "enqueuePeriodic: " + simpleName);
        w b2 = new w.a(cls, 900000L, TimeUnit.MILLISECONDS).b();
        K.o(b2, "build(...)");
        D.p(context).l(simpleName, AppConfigKt.getAppConfig().isWidgetFixEnable() ? w5.g.KEEP : w5.g.REPLACE, b2);
    }
}
