package com.mihoyo.hyperion.biz.miniwidget;

import G50.l;
import G50.m;
import Lx.B;
import Lx.G;
import Oy.d;
import Oy.f;
import Tx.o;
import android.content.Context;
import androidx.work.CoroutineWorker;
import androidx.work.ListenableWorker;
import androidx.work.WorkerParameters;
import com.mihoyo.commlib.utils.LogUtils;
import com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker;
import com.mihoyo.hyperion.chat.bean.MessageReplyInfo;
import java.util.concurrent.TimeUnit;
import kotlin.jvm.functions.Function1;
import kotlin.jvm.internal.DefaultConstructorMarker;
import kotlin.jvm.internal.K;
import x0.v;

@v(parameters = 0)
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public abstract class BaseWidgetWorker extends CoroutineWorker {
    public static final int j = 8;

    @l
    public final Context i;

    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class a implements o<B<Throwable>, G<?>> {

        @l
        public static final C0037a b = new C0037a(null);
        public static final int c = 3;
        public static final long d = 3000;
        public int a;

        /* renamed from: com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker$a$a, reason: collision with other inner class name */
        /* loaded from: X:\AI project\解包\tools\classes12.dex */
        public static final class C0037a {
            public /* synthetic */ C0037a(DefaultConstructorMarker defaultConstructorMarker) {
                this();
            }

            public C0037a() {
            }
        }

        public static final G d(a aVar, Throwable th2) {
            int i;
            K.p(th2, "throwable");
            if ((th2 instanceof ne.a) || (i = aVar.a) >= 3) {
                return B.f2(th2);
            }
            aVar.a = i + 1;
            LogUtils.INSTANCE.i("widget-x", "retry request, e: " + th2.getMessage());
            return B.P6(d, TimeUnit.MILLISECONDS);
        }

        public static final G e(Function1 function1, Object obj) {
            K.p(obj, "p0");
            return (G) function1.invoke(obj);
        }

        @l
        /* renamed from: c, reason: merged with bridge method [inline-methods] */
        public G<?> apply(@l B<Throwable> b2) {
            K.p(b2, "t");
            final Function1 function1 = new Function1() { // from class: hg.e
                public final Object invoke(Object obj) {
                    G d2;
                    d2 = BaseWidgetWorker.a.d(BaseWidgetWorker.a.this, (Throwable) obj);
                    return d2;
                }
            };
            B l2 = b2.l2(new o() { // from class: hg.f
                public final Object apply(Object obj) {
                    G e;
                    e = BaseWidgetWorker.a.e(function1, obj);
                    return e;
                }
            });
            K.o(l2, "flatMap(...)");
            return l2;
        }
    }

    @f(c = "com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker", f = "BaseWidgetWorker.kt", i = {}, l = {MessageReplyInfo.TYPE_LIKE_COMMENT}, m = "doWork$suspendImpl", n = {}, s = {})
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class b extends d {
        public /* synthetic */ Object a;
        public int c;

        public b(Ly.f<? super b> fVar) {
            super(fVar);
        }

        @m
        public final Object invokeSuspend(@l Object obj) {
            this.a = obj;
            this.c |= Integer.MIN_VALUE;
            return BaseWidgetWorker.H(BaseWidgetWorker.this, this);
        }
    }

    /* JADX WARN: 'super' call moved to the top of the method (can break code semantics) */
    public BaseWidgetWorker(@l Context context, @l WorkerParameters workerParameters) {
        super(context, workerParameters);
        K.p(context, "context");
        K.p(workerParameters, "workerParameters");
        this.i = context;
    }

    /* JADX WARN: Removed duplicated region for block: B:12:0x0047  */
    /* JADX WARN: Removed duplicated region for block: B:15:0x004f  */
    /* JADX WARN: Removed duplicated region for block: B:19:0x0031  */
    /* JADX WARN: Removed duplicated region for block: B:8:0x0023  */
    /*
        Code decompiled incorrectly, please refer to instructions dump.
        To view partially-correct add '--show-bad-code' argument
    */
    public static /* synthetic */ java.lang.Object H(com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker r4, Ly.f<? super androidx.work.ListenableWorker.a> r5) {
        /*
            boolean r0 = r5 instanceof com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker.b
            if (r0 == 0) goto L13
            r0 = r5
            com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker$b r0 = (com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker.b) r0
            int r1 = r0.c
            r2 = -2147483648(0xffffffff80000000, float:-0.0)
            r3 = r1 & r2
            if (r3 == 0) goto L13
            int r1 = r1 - r2
            r0.c = r1
            goto L18
        L13:
            com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker$b r0 = new com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker$b
            r0.<init>(r5)
        L18:
            java.lang.Object r5 = r0.a
            java.lang.Object r1 = Ny.d.l()
            int r2 = r0.c
            r3 = 1
            if (r2 == 0) goto L31
            if (r2 != r3) goto L29
            zy.f0.n(r5)
            goto L3f
        L29:
            java.lang.IllegalStateException r4 = new java.lang.IllegalStateException
            java.lang.String r5 = "call to 'resume' before 'invoke' with coroutine"
            r4.<init>(r5)
            throw r4
        L31:
            zy.f0.n(r5)
            android.content.Context r5 = r4.i
            r0.c = r3
            java.lang.Object r5 = r4.I(r5, r0)
            if (r5 != r1) goto L3f
            return r1
        L3f:
            java.lang.Boolean r5 = (java.lang.Boolean) r5
            boolean r4 = r5.booleanValue()
            if (r4 == 0) goto L4f
            androidx.work.ListenableWorker$a r4 = androidx.work.ListenableWorker.a.e()
            kotlin.jvm.internal.K.m(r4)
            goto L56
        L4f:
            androidx.work.ListenableWorker$a r4 = androidx.work.ListenableWorker.a.a()
            kotlin.jvm.internal.K.m(r4)
        L56:
            return r4
        */
        throw new UnsupportedOperationException("Method not decompiled: com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker.H(com.mihoyo.hyperion.biz.miniwidget.BaseWidgetWorker, Ly.f):java.lang.Object");
    }

    @m
    public abstract Object I(@l Context context, @l Ly.f<? super Boolean> fVar);

    @m
    public Object y(@l Ly.f<? super ListenableWorker.a> fVar) {
        return H(this, fVar);
    }
}
