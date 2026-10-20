package rg;

import Ce.P;
import G50.l;
import G50.m;
import Oy.d;
import Oy.f;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapShader;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PorterDuff;
import android.graphics.PorterDuffXfermode;
import android.graphics.Rect;
import android.graphics.RectF;
import android.graphics.Shader;
import android.util.Size;
import com.mihoyo.commlib.utils.LogUtils;
import kotlin.jvm.internal.K;
import kotlin.jvm.internal.r0;
import x0.v;

@v(parameters = 0)
@r0({"SMAP\nNapImageManager.kt\nKotlin\n*S Kotlin\n*F\n+ 1 NapImageManager.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/utils/NapImageManager\n+ 2 Bitmap.kt\nandroidx/core/graphics/BitmapKt\n*L\n1#1,241:1\n90#2,6:242\n95#2:248\n*S KotlinDebug\n*F\n+ 1 NapImageManager.kt\ncom/mihoyo/hyperion/biz/miniwidget/napavatar/utils/NapImageManager\n*L\n60#1:242,6\n101#1:248\n*E\n"})
/* renamed from: rg.b, reason: case insensitive filesystem */
/* loaded from: X:\AI project\解包\tools\classes12.dex */
public final class C0793b {

    @l
    public static final C0793b a = new C0793b();

    @l
    public static final float[] b = {P.a0(12), P.a0(12), P.a0(12), P.a0(12), 0.0f, 0.0f, 0.0f, 0.0f};
    public static final int c = 8;

    @f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.utils.NapImageManager", f = "NapImageManager.kt", i = {0, 0}, l = {42}, m = "getWidgetBackgroundBitmapById", n = {"this", "radii"}, s = {"L$0", "L$1"})
    /* renamed from: rg.b$a */
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class a extends d {
        public Object a;
        public Object b;
        public /* synthetic */ Object c;
        public int e;

        public a(Ly.f<? super a> fVar) {
            super(fVar);
        }

        @m
        public final Object invokeSuspend(@l Object obj) {
            this.c = obj;
            this.e |= Integer.MIN_VALUE;
            return C0793b.this.e(null, 0, null, null, this);
        }
    }

    @f(c = "com.mihoyo.hyperion.biz.miniwidget.napavatar.utils.NapImageManager", f = "NapImageManager.kt", i = {0, 0, 0, 0, 0}, l = {228, 230}, m = "loadBitmap", n = {"this", "context", "overrideSize", "radii", "feedbackId"}, s = {"L$0", "L$1", "L$2", "L$3", "I$0"})
    /* renamed from: rg.b$b, reason: collision with other inner class name */
    /* loaded from: X:\AI project\解包\tools\classes12.dex */
    public static final class C0094b extends d {
        public Object a;
        public Object b;
        public Object c;
        public Object d;
        public int e;
        public /* synthetic */ Object f;
        public int h;

        public C0094b(Ly.f<? super C0094b> fVar) {
            super(fVar);
        }

        @m
        public final Object invokeSuspend(@l Object obj) {
            this.f = obj;
            this.h |= Integer.MIN_VALUE;
            return C0793b.this.g(null, null, 0, null, null, this);
        }
    }

    public static /* synthetic */ Bitmap b(C0793b c0793b, Bitmap bitmap, float f, float f2, int i, Object obj) {
        if ((i & 1) != 0) {
            f = P.a0(10);
        }
        if ((i & 2) != 0) {
            f2 = 0.0f;
        }
        return c0793b.a(bitmap, f, f2);
    }

    public static /* synthetic */ Object f(C0793b c0793b, Context context, int i, Size size, float[] fArr, Ly.f fVar, int i2, Object obj) {
        if ((i2 & 4) != 0) {
            size = null;
        }
        Size size2 = size;
        if ((i2 & 8) != 0) {
            fArr = b;
        }
        return c0793b.e(context, i, size2, fArr, fVar);
    }

    public static /* synthetic */ Object h(C0793b c0793b, Context context, String str, int i, Size size, float[] fArr, Ly.f fVar, int i2, Object obj) {
        if ((i2 & 4) != 0) {
            i = 0;
        }
        int i3 = i;
        if ((i2 & 8) != 0) {
            size = null;
        }
        Size size2 = size;
        if ((i2 & 16) != 0) {
            fArr = b;
        }
        return c0793b.g(context, str, i3, size2, fArr, fVar);
    }

    @l
    public final Bitmap a(@l Bitmap bitmap, float f, float f2) {
        K.p(bitmap, "<this>");
        Bitmap createBitmap = Bitmap.createBitmap(bitmap.getWidth() + ((int) f), bitmap.getHeight() + ((int) f2), Bitmap.Config.ARGB_8888);
        K.o(createBitmap, "createBitmap(...)");
        Canvas canvas = new Canvas(createBitmap);
        canvas.save();
        canvas.translate(f, f2);
        Paint paint = new Paint(1);
        int argb = Color.argb(76, 0, 0, 0);
        paint.setShader(new LinearGradient(0.0f, 0.0f, bitmap.getWidth(), 0.0f, new int[]{0, 0, argb, argb}, new float[]{0.0f, 0.05f, 0.2f, 1.0f}, Shader.TileMode.CLAMP));
        canvas.drawRect(0.0f, 0.0f, bitmap.getWidth(), bitmap.getHeight(), paint);
        paint.setXfermode(new PorterDuffXfermode(PorterDuff.Mode.DST_IN));
        canvas.drawBitmap(bitmap, 0.0f, 0.0f, paint);
        canvas.restore();
        canvas.drawBitmap(bitmap, 0.0f, 0.0f, (Paint) null);
        return createBitmap;
    }

    @m
    public final Bitmap c(@l Bitmap bitmap, @m Bitmap bitmap2, @m Bitmap bitmap3, @m Bitmap bitmap4) {
        K.p(bitmap, "bgBitmap");
        if (bitmap2 == null || bitmap3 == null || bitmap4 == null) {
            return null;
        }
        try {
            long currentTimeMillis = System.currentTimeMillis();
            Bitmap.Config config = Bitmap.Config.ARGB_8888;
            int width = bitmap.getWidth();
            int height = bitmap.getHeight();
            if (width != 0 && height != 0) {
                Bitmap createBitmap = Bitmap.createBitmap(width, height, config);
                Canvas canvas = new Canvas(createBitmap);
                canvas.drawBitmap(bitmap, 0.0f, 0.0f, (Paint) null);
                canvas.drawBitmap(bitmap2, 0.0f, P.a0(56), (Paint) null);
                canvas.drawBitmap(bitmap3, 0.0f, 0.0f, (Paint) null);
                canvas.drawBitmap(bitmap4, 0.0f, bitmap.getHeight() - bitmap4.getHeight(), (Paint) null);
                Bitmap j = j(createBitmap, P.a0(22));
                createBitmap.recycle();
                LogUtils.INSTANCE.d("combineTime = " + (System.currentTimeMillis() - currentTimeMillis));
                return j;
            }
        } catch (Throwable unused) {
        }
        return null;
    }

    @m
    public final Bitmap d(int i, int i2, int i3) {
        try {
            Bitmap createBitmap = Bitmap.createBitmap(i2, i3, Bitmap.Config.ARGB_8888);
            Canvas canvas = new Canvas(createBitmap);
            Paint paint = new Paint();
            float f = i3;
            paint.setShader(new LinearGradient(0.0f, 0.0f, 0.0f, f, i & 16777215, i, Shader.TileMode.CLAMP));
            canvas.drawRect(0.0f, 0.0f, i2, f, paint);
            return createBitmap;
        } catch (Exception e) {
            LogUtils.INSTANCE.e("createGradientBitmap error: " + e.getMessage());
            return null;
        }
    }

    /* JADX WARN: Removed duplicated region for block: B:13:0x0068 A[RETURN] */
    /* JADX WARN: Removed duplicated region for block: B:15:0x006a  */
    /* JADX WARN: Removed duplicated region for block: B:21:0x0042  */
    /* JADX WARN: Removed duplicated region for block: B:9:0x0028  */
    @G50.m
    /*
        Code decompiled incorrectly, please refer to instructions dump.
        To view partially-correct add '--show-bad-code' argument
    */
    public final java.lang.Object e(@G50.l android.content.Context r15, @i.v int r16, @G50.m android.util.Size r17, @G50.m float[] r18, @G50.l Ly.f<? super android.graphics.Bitmap> r19) {
        /*
            r14 = this;
            r0 = r14
            r1 = r19
            boolean r2 = r1 instanceof rg.C0793b.a
            if (r2 == 0) goto L17
            r2 = r1
            rg.b$a r2 = (rg.C0793b.a) r2
            int r3 = r2.e
            r4 = -2147483648(0xffffffff80000000, float:-0.0)
            r5 = r3 & r4
            if (r5 == 0) goto L17
            int r3 = r3 - r4
            r2.e = r3
        L15:
            r10 = r2
            goto L1d
        L17:
            rg.b$a r2 = new rg.b$a
            r2.<init>(r1)
            goto L15
        L1d:
            java.lang.Object r1 = r10.c
            java.lang.Object r2 = Ny.d.l()
            int r3 = r10.e
            r4 = 1
            if (r3 == 0) goto L42
            if (r3 != r4) goto L3a
            java.lang.Object r2 = r10.b
            float[] r2 = (float[]) r2
            java.lang.Object r3 = r10.a
            rg.b r3 = (rg.C0793b) r3
            zy.f0.n(r1)
            r13 = r3
            r3 = r1
            r1 = r2
            r2 = r13
            goto L64
        L3a:
            java.lang.IllegalStateException r1 = new java.lang.IllegalStateException
            java.lang.String r2 = "call to 'resume' before 'invoke' with coroutine"
            r1.<init>(r2)
            throw r1
        L42:
            zy.f0.n(r1)
            se.n r3 = se.n.a
            java.lang.Integer r5 = Oy.b.f(r16)
            r10.a = r0
            r1 = r18
            r10.b = r1
            r10.e = r4
            r6 = 0
            r7 = 0
            r9 = 0
            r11 = 44
            r12 = 0
            r4 = r15
            r8 = r17
            java.lang.Object r3 = se.n.G(r3, r4, r5, r6, r7, r8, r9, r10, r11, r12)
            if (r3 != r2) goto L63
            return r2
        L63:
            r2 = r0
        L64:
            android.graphics.Bitmap r3 = (android.graphics.Bitmap) r3
            if (r3 != 0) goto L6a
            r1 = 0
            return r1
        L6a:
            if (r1 == 0) goto L71
            android.graphics.Bitmap r1 = r2.i(r3, r1)
            return r1
        L71:
            return r3
        */
        throw new UnsupportedOperationException("Method not decompiled: rg.C0793b.e(android.content.Context, int, android.util.Size, float[], Ly.f):java.lang.Object");
    }

    /* JADX WARN: Removed duplicated region for block: B:24:0x00c1  */
    /* JADX WARN: Removed duplicated region for block: B:26:0x00cb  */
    /* JADX WARN: Removed duplicated region for block: B:30:0x0054  */
    /* JADX WARN: Removed duplicated region for block: B:8:0x0028  */
    @G50.m
    /*
        Code decompiled incorrectly, please refer to instructions dump.
        To view partially-correct add '--show-bad-code' argument
    */
    public final java.lang.Object g(@G50.l android.content.Context r20, @G50.l java.lang.String r21, @i.v int r22, @G50.m android.util.Size r23, @G50.m float[] r24, @G50.l Ly.f<? super zy.V<android.graphics.Bitmap, java.lang.Boolean>> r25) {
        /*
            Method dump skipped, instructions count: 219
            To view this dump add '--comments-level debug' option
        */
        throw new UnsupportedOperationException("Method not decompiled: rg.C0793b.g(android.content.Context, java.lang.String, int, android.util.Size, float[], Ly.f):java.lang.Object");
    }

    public final Bitmap i(Bitmap bitmap, float[] fArr) {
        Bitmap createBitmap = Bitmap.createBitmap(bitmap.getWidth(), bitmap.getHeight(), Bitmap.Config.ARGB_8888);
        K.o(createBitmap, "createBitmap(...)");
        Canvas canvas = new Canvas(createBitmap);
        Shader.TileMode tileMode = Shader.TileMode.CLAMP;
        BitmapShader bitmapShader = new BitmapShader(bitmap, tileMode, tileMode);
        Paint paint = new Paint();
        paint.setAntiAlias(true);
        paint.setShader(bitmapShader);
        RectF rectF = new RectF(0.0f, 0.0f, createBitmap.getWidth(), createBitmap.getHeight());
        Path path = new Path();
        path.addRoundRect(rectF, fArr, Path.Direction.CW);
        canvas.drawPath(path, paint);
        return createBitmap;
    }

    @l
    public final Bitmap j(@l Bitmap bitmap, float f) {
        K.p(bitmap, "srcBmp");
        Bitmap createBitmap = Bitmap.createBitmap(bitmap.getWidth(), bitmap.getHeight(), Bitmap.Config.ARGB_8888);
        K.o(createBitmap, "createBitmap(...)");
        Canvas canvas = new Canvas(createBitmap);
        Paint paint = new Paint();
        paint.setAntiAlias(true);
        RectF rectF = new RectF(new Rect(0, 0, canvas.getWidth(), canvas.getHeight()));
        Shader.TileMode tileMode = Shader.TileMode.CLAMP;
        paint.setShader(new BitmapShader(bitmap, tileMode, tileMode));
        canvas.drawRoundRect(rectF, f, f, paint);
        return createBitmap;
    }
}
